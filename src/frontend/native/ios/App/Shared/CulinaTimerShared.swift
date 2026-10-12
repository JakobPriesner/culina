// Compiled into the app and the CulinaTimerWidget extension: what both need to agree on.
// docs/native-app-design.md, part 4.

import ActivityKit
import AppIntents
import Foundation
#if canImport(AlarmKit)
import AlarmKit
#endif

/// One kitchen timer as the page sends it (src/lib/features/cooking/timerState.ts `KitchenTimer`).
public struct CulinaTimer: Codable, Hashable, Sendable {
    public var stepIndex: Int
    public var label: String
    /// Epoch milliseconds.
    public var endsAt: Double
    /// Seconds left while paused; a paused timer rings nowhere.
    public var pausedRemaining: Double?

    public var isRunning: Bool { pausedRemaining == nil }

    public var deadline: Date { Date(timeIntervalSince1970: endsAt / 1000) }
}

/// The words the system shows, in the app's language: the page sends them, as the app has no catalogue of its own.
public struct CulinaTimerText: Codable, Hashable, Sendable {
    public var timer: String
    public var finished: String
    public var minute1: String
    public var minute2: String
    public var dismiss: String
    public var next: String
    public var paused: String
    /// With `{count}` for the number of further timers.
    public var more: String

    public func more(_ count: Int) -> String {
        more.replacingOccurrences(of: "{count}", with: String(count))
    }
}

/// The Live Activity of one cooking session: its timers, soonest first.
@available(iOS 16.1, *)
public struct CulinaTimerAttributes: ActivityAttributes {
    public struct ContentState: Codable, Hashable {
        public var timers: [CulinaTimer]
        public var text: CulinaTimerText

        public init(timers: [CulinaTimer], text: CulinaTimerText) {
            self.timers = timers
            self.text = text
        }
    }

    public var sessionId: String

    public init(sessionId: String) {
        self.sessionId = sessionId
    }
}

#if canImport(AlarmKit)
/// What an AlarmKit timer carries for its widget: which step of which session it is.
@available(iOS 26.0, *)
public struct CulinaAlarmMetadata: AlarmMetadata {
    public var sessionId: String
    public var stepIndex: Int
    public var label: String

    public init(sessionId: String, stepIndex: Int, label: String) {
        self.sessionId = sessionId
        self.stepIndex = stepIndex
        self.label = label
    }
}
#endif

/// One button press on the lock screen: applied by the app at once, then by the page when it next runs.
public struct CulinaTimerPress: Codable, Hashable, Sendable {
    public var sessionId: String
    public var stepIndex: Int
    /// The deadline the button was shown for; a press for an older one changes nothing.
    public var endsAt: Double
    public var url: String
    /// `minute-1`, `minute-2`, `dismiss`, `next` or `open`.
    public var action: String
    /// When it was pressed, epoch milliseconds.
    public var at: Double

    public init(sessionId: String, stepIndex: Int, endsAt: Double, url: String, action: String, at: Double) {
        self.sessionId = sessionId
        self.stepIndex = stepIndex
        self.endsAt = endsAt
        self.url = url
        self.action = action
        self.at = at
    }
}

/// Where a press goes. Set by the app at launch (AppDelegate); a Live Activity intent runs in the app's process, so the
/// extension, which compiles the intent too, never calls it.
public enum CulinaTimerSink {
    nonisolated(unsafe) public static var handler: (@Sendable (CulinaTimerPress) async -> Void)?

    public static func handle(_ press: CulinaTimerPress) async {
        await handler?(press)
    }
}

/// A button on the Live Activity or an AlarmKit alert.
@available(iOS 17.0, *)
public struct CulinaTimerButtonIntent: LiveActivityIntent {
    public static let title: LocalizedStringResource = "Kitchen timer"
    public static let isDiscoverable = false

    @Parameter(title: "Session")
    public var sessionId: String

    @Parameter(title: "Step")
    public var stepIndex: Int

    @Parameter(title: "Deadline")
    public var endsAt: Double

    @Parameter(title: "Action")
    public var action: String

    public init() {}

    public init(sessionId: String, stepIndex: Int, endsAt: Double, action: String) {
        self.sessionId = sessionId
        self.stepIndex = stepIndex
        self.endsAt = endsAt
        self.action = action
    }

    public func perform() async throws -> some IntentResult {
        await CulinaTimerSink.handle(CulinaTimerPress(
            sessionId: sessionId,
            stepIndex: stepIndex,
            endsAt: endsAt,
            url: "",
            action: action,
            at: Date().timeIntervalSince1970 * 1000))

        return .result()
    }
}
