// The app's kitchen timers on iOS (docs/native-app-design.md, 4.3). The page hands over the whole set of timers whenever
// it changes; this keeps the system's alarms and the Live Activity exactly that set, and applies a lock-screen press
// at once, recording it for the page to apply too (src/lib/features/cooking/nativeTimers.ts).
//
// iOS 26 and later, with the person's permission: AlarmKit, which rings through the silent switch and Focus and draws
// its own countdown (CulinaTimerWidget's alarm activity). Otherwise: a time-sensitive notification per timer and one
// Live Activity per cooking session.

import ActivityKit
import Foundation
import UserNotifications
#if canImport(AlarmKit)
import AlarmKit
#endif

/// What the page sent last: kept, so a press with the app closed can be applied and the alarms rebuilt.
struct CulinaTimerSnapshot: Codable {
    var sessionId: String
    var url: String
    var timers: [CulinaTimer]
    var text: CulinaTimerText
}

@MainActor
final class CulinaTimerEngine {
    static let shared = CulinaTimerEngine()

    /// Called after a press was recorded, so a running page drains it at once.
    var onPress: (() -> Void)?

    nonisolated static let category = "culina-timer"
    private static let snapshotKey = "culina.timers.snapshot"
    private static let journalKey = "culina.timers.journal"
    private static let alarmsKey = "culina.timers.alarms"

    private let defaults = UserDefaults.standard
    private let notifications = UNUserNotificationCenter.current()

    private init() {}

    // MARK: The page's side

    /// Makes what the system rings and shows exactly `snapshot`'s timers; nil ends everything.
    func sync(_ snapshot: CulinaTimerSnapshot?) async {
        store(snapshot)
        registerCategory(snapshot?.text)
        await present(snapshot)
    }

    /// Hands over and forgets every recorded press.
    func drain() -> [CulinaTimerPress] {
        let presses = journal()
        defaults.removeObject(forKey: Self.journalKey)

        return presses
    }

    /// Takes finished timers' notifications off the screen.
    func close(sessionId: String, stepIndex: Int?) async {
        let delivered = await notifications.deliveredNotifications()
        let ids = delivered
            .filter { notification in
                let info = notification.request.content.userInfo
                return info["sessionId"] as? String == sessionId
                    && (stepIndex == nil || info["stepIndex"] as? Int == stepIndex)
            }
            .map(\.request.identifier)

        notifications.removeDeliveredNotifications(withIdentifiers: ids)
    }

    func requestPermissions() async -> Bool {
        let granted = (try? await notifications.requestAuthorization(options: [.alert, .sound, .badge])) ?? false

        #if canImport(AlarmKit)
        if #available(iOS 26.0, *), AlarmManager.shared.authorizationState == .notDetermined {
            _ = try? await AlarmManager.shared.requestAuthorization()
        }
        #endif

        return granted
    }

    // MARK: The lock screen's side

    /// A press on a notification, the Live Activity or an alarm: applied to the kept timers now, recorded for the page.
    func handle(_ press: CulinaTimerPress) async {
        guard var snapshot = snapshot(), snapshot.sessionId == press.sessionId else {
            return
        }

        var recorded = press
        recorded.url = snapshot.url

        if let index = snapshot.timers.firstIndex(where: {
            $0.stepIndex == press.stepIndex && $0.endsAt == press.endsAt && $0.isRunning
        }) {
            switch press.action {
            case "minute-1":
                snapshot.timers[index].endsAt = press.at + 60_000
            case "minute-2":
                snapshot.timers[index].endsAt = press.at + 120_000
            case "dismiss", "next":
                snapshot.timers.remove(at: index)
            default:
                break
            }
        }

        record(recorded)
        store(snapshot)
        await present(snapshot)
        await close(sessionId: press.sessionId, stepIndex: press.stepIndex)
        onPress?()
    }

    /// A tap on a timer's notification: the page opens the recipe.
    func opened(sessionId: String, stepIndex: Int, endsAt: Double) {
        guard let snapshot = snapshot(), snapshot.sessionId == sessionId else {
            return
        }

        record(CulinaTimerPress(
            sessionId: sessionId,
            stepIndex: stepIndex,
            endsAt: endsAt,
            url: snapshot.url,
            action: "open",
            at: Date().timeIntervalSince1970 * 1000))
        onPress?()
    }

    // MARK: Presenting

    private func present(_ snapshot: CulinaTimerSnapshot?) async {
        let running = (snapshot?.timers ?? []).filter { $0.isRunning && $0.deadline > Date() }

        #if canImport(AlarmKit)
        if #available(iOS 26.0, *), AlarmManager.shared.authorizationState == .authorized {
            await presentAlarms(snapshot, running: running)
            await presentNotifications(nil, running: [])
            await presentActivity(nil)

            return
        }
        #endif

        await presentNotifications(snapshot, running: running)
        await presentActivity(snapshot)
    }

    private func presentNotifications(_ snapshot: CulinaTimerSnapshot?, running: [CulinaTimer]) async {
        let pending = await notifications.pendingNotificationRequests()
            .filter { $0.content.categoryIdentifier == Self.category }
        let wanted = Dictionary(uniqueKeysWithValues: running.map { (identifier(snapshot!.sessionId, $0.stepIndex), $0) })

        let stale = pending.filter { request in
            guard let timer = wanted[request.identifier] else { return true }
            return request.content.userInfo["endsAt"] as? Double != timer.endsAt
        }
        notifications.removePendingNotificationRequests(withIdentifiers: stale.map(\.identifier))

        guard let snapshot else { return }

        let kept = Set(pending.map(\.identifier)).subtracting(stale.map(\.identifier))

        for (id, timer) in wanted where !kept.contains(id) {
            let content = UNMutableNotificationContent()
            content.title = timer.label.isEmpty ? snapshot.text.timer : timer.label
            content.body = snapshot.text.finished
            content.sound = .default
            content.categoryIdentifier = Self.category
            content.interruptionLevel = .timeSensitive
            content.threadIdentifier = "culina-timer-\(snapshot.sessionId)"
            content.userInfo = [
                "sessionId": snapshot.sessionId,
                "stepIndex": timer.stepIndex,
                "endsAt": timer.endsAt
            ]

            let trigger = UNTimeIntervalNotificationTrigger(
                timeInterval: max(1, timer.deadline.timeIntervalSinceNow),
                repeats: false)

            try? await notifications.add(UNNotificationRequest(identifier: id, content: content, trigger: trigger))
        }
    }

    private func presentActivity(_ snapshot: CulinaTimerSnapshot?) async {
        guard #available(iOS 16.2, *) else { return }

        let timers = (snapshot?.timers ?? [])
            .filter { !$0.isRunning || $0.deadline > Date() }
            .sorted { ($0.pausedRemaining ?? .infinity, $0.endsAt) < ($1.pausedRemaining ?? .infinity, $1.endsAt) }

        for activity in Activity<CulinaTimerAttributes>.activities
        where activity.attributes.sessionId != snapshot?.sessionId || timers.isEmpty {
            await activity.end(nil, dismissalPolicy: .immediate)
        }

        guard let snapshot, !timers.isEmpty, ActivityAuthorizationInfo().areActivitiesEnabled else { return }

        let state = CulinaTimerAttributes.ContentState(timers: timers, text: snapshot.text)
        // Past the soonest deadline the countdown reads 0:00; stale lets the view say "finished" instead.
        let stale = timers.first(where: \.isRunning)?.deadline
        let content = ActivityContent(state: state, staleDate: stale)

        if let activity = Activity<CulinaTimerAttributes>.activities.first(where: {
            $0.attributes.sessionId == snapshot.sessionId
        }) {
            await activity.update(content)
        } else {
            _ = try? Activity.request(
                attributes: CulinaTimerAttributes(sessionId: snapshot.sessionId),
                content: content,
                pushType: nil)
        }
    }

    #if canImport(AlarmKit)
    @available(iOS 26.0, *)
    private func presentAlarms(_ snapshot: CulinaTimerSnapshot?, running: [CulinaTimer]) async {
        // Scheduled alarm id -> the deadline it rings at; AlarmKit does not say which timer an alarm was for.
        var scheduled = (defaults.dictionary(forKey: Self.alarmsKey) as? [String: Double]) ?? [:]
        let wanted = Set(running.map(\.endsAt))

        for (id, endsAt) in scheduled where !wanted.contains(endsAt) {
            if let uuid = UUID(uuidString: id) {
                try? AlarmManager.shared.cancel(id: uuid)
            }
            scheduled[id] = nil
        }

        if let snapshot {
            let present = Set(scheduled.values)

            for timer in running where !present.contains(timer.endsAt) {
                let id = UUID()

                if (try? await AlarmManager.shared.schedule(
                    id: id,
                    configuration: configuration(for: timer, in: snapshot))) != nil {
                    scheduled[id.uuidString] = timer.endsAt
                }
            }
        }

        defaults.set(scheduled, forKey: Self.alarmsKey)
    }

    @available(iOS 26.0, *)
    private func configuration(
        for timer: CulinaTimer,
        in snapshot: CulinaTimerSnapshot
    ) -> AlarmManager.AlarmConfiguration<CulinaAlarmMetadata> {
        let title = LocalizedStringResource(stringLiteral: timer.label.isEmpty ? snapshot.text.timer : timer.label)
        let alert = AlarmPresentation.Alert(
            title: title,
            stopButton: AlarmButton(
                text: LocalizedStringResource(stringLiteral: snapshot.text.dismiss),
                textColor: .white,
                systemImageName: "xmark"),
            secondaryButton: AlarmButton(
                text: LocalizedStringResource(stringLiteral: snapshot.text.minute1),
                textColor: .white,
                systemImageName: "plus"),
            secondaryButtonBehavior: .custom)
        let presentation = AlarmPresentation(alert: alert, countdown: AlarmPresentation.Countdown(title: title))
        let attributes = AlarmAttributes(
            presentation: presentation,
            metadata: CulinaAlarmMetadata(sessionId: snapshot.sessionId, stepIndex: timer.stepIndex, label: timer.label),
            tintColor: .orange)

        return .timer(
            duration: max(1, timer.deadline.timeIntervalSinceNow),
            attributes: attributes,
            stopIntent: CulinaTimerButtonIntent(
                sessionId: snapshot.sessionId, stepIndex: timer.stepIndex, endsAt: timer.endsAt, action: "dismiss"),
            secondaryIntent: CulinaTimerButtonIntent(
                sessionId: snapshot.sessionId, stepIndex: timer.stepIndex, endsAt: timer.endsAt, action: "minute-1"),
            sound: .default)
    }
    #endif

    // MARK: Kept state

    private func registerCategory(_ text: CulinaTimerText?) {
        guard let text else { return }

        let actions = [
            // In the background: the app applies these itself, without opening.
            UNNotificationAction(identifier: "minute-1", title: text.minute1, options: []),
            UNNotificationAction(identifier: "minute-2", title: text.minute2, options: []),
            UNNotificationAction(identifier: "dismiss", title: text.dismiss, options: [.destructive]),
            // In the foreground: the next step is read in the app.
            UNNotificationAction(identifier: "next", title: text.next, options: [.foreground])
        ]

        notifications.setNotificationCategories([
            UNNotificationCategory(identifier: Self.category, actions: actions, intentIdentifiers: [])
        ])
    }

    private func identifier(_ sessionId: String, _ stepIndex: Int) -> String {
        "culina.timer.\(sessionId).\(stepIndex)"
    }

    private func snapshot() -> CulinaTimerSnapshot? {
        defaults.data(forKey: Self.snapshotKey).flatMap { try? JSONDecoder().decode(CulinaTimerSnapshot.self, from: $0) }
    }

    private func store(_ snapshot: CulinaTimerSnapshot?) {
        if let snapshot, let data = try? JSONEncoder().encode(snapshot) {
            defaults.set(data, forKey: Self.snapshotKey)
        } else {
            defaults.removeObject(forKey: Self.snapshotKey)
        }
    }

    private func journal() -> [CulinaTimerPress] {
        defaults.data(forKey: Self.journalKey).flatMap { try? JSONDecoder().decode([CulinaTimerPress].self, from: $0) }
            ?? []
    }

    private func record(_ press: CulinaTimerPress) {
        if let data = try? JSONEncoder().encode(journal() + [press]) {
            defaults.set(data, forKey: Self.journalKey)
        }
    }
}

/// The notification delegate for timer notifications, for when the system wakes the app for a button with no web view
/// (AppDelegate). Once Capacitor's bridge loads it routes to the plugin, which forwards here.
final class CulinaTimerNotifications: NSObject, UNUserNotificationCenterDelegate, Sendable {
    static let shared = CulinaTimerNotifications()

    func userNotificationCenter(
        _ center: UNUserNotificationCenter,
        willPresent notification: UNNotification
    ) async -> UNNotificationPresentationOptions {
        Self.presentation(for: notification)
    }

    func userNotificationCenter(
        _ center: UNUserNotificationCenter,
        didReceive response: UNNotificationResponse
    ) async {
        await Self.receive(response)
    }

    /// Shown and sounded even with the app open: the page rings only a deadline it saw pass on screen.
    static func presentation(for notification: UNNotification) -> UNNotificationPresentationOptions {
        notification.request.content.categoryIdentifier == CulinaTimerEngine.category ? [.banner, .sound, .list] : []
    }

    static func receive(_ response: UNNotificationResponse) async {
        let info = response.notification.request.content.userInfo

        guard response.notification.request.content.categoryIdentifier == CulinaTimerEngine.category,
              let sessionId = info["sessionId"] as? String,
              let stepIndex = info["stepIndex"] as? Int,
              let endsAt = info["endsAt"] as? Double else {
            return
        }

        let engine = await CulinaTimerEngine.shared

        switch response.actionIdentifier {
        case UNNotificationDefaultActionIdentifier:
            await engine.opened(sessionId: sessionId, stepIndex: stepIndex, endsAt: endsAt)
        case UNNotificationDismissActionIdentifier:
            break
        default:
            await engine.handle(CulinaTimerPress(
                sessionId: sessionId,
                stepIndex: stepIndex,
                endsAt: endsAt,
                url: "",
                action: response.actionIdentifier,
                at: Date().timeIntervalSince1970 * 1000))
        }
    }
}
