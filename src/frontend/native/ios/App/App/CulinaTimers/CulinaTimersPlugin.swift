// `CulinaTimers` for the page (src/lib/features/cooking/nativeTimers.ts). Registered by MainViewController, not npm:
// it is part of this app.

import Capacitor
import Foundation
import UserNotifications

@objc(CulinaTimersPlugin)
public class CulinaTimersPlugin: CAPPlugin, CAPBridgedPlugin, NotificationHandlerProtocol {
    public let identifier = "CulinaTimersPlugin"
    public let jsName = "CulinaTimers"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "sync", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "drain", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "close", returnType: CAPPluginReturnPromise),
        CAPPluginMethod(name: "requestPermissions", returnType: CAPPluginReturnPromise)
    ]

    override public func load() {
        // The local-notification slot of Capacitor's router: no other plugin in this app claims it.
        bridge?.notificationRouter.localNotificationHandler = self

        Task { @MainActor [weak self] in
            CulinaTimerEngine.shared.onPress = { [weak self] in
                self?.notifyListeners("action", data: [:])
            }
        }
    }

    @objc func sync(_ call: CAPPluginCall) {
        let snapshot = decode(call)

        Task { @MainActor in
            await CulinaTimerEngine.shared.sync(snapshot)
            call.resolve()
        }
    }

    @objc func drain(_ call: CAPPluginCall) {
        Task { @MainActor in
            let presses = CulinaTimerEngine.shared.drain()
            let actions = presses.map { press -> [String: Any] in
                [
                    "sessionId": press.sessionId,
                    "stepIndex": press.stepIndex,
                    "endsAt": press.endsAt,
                    "url": press.url,
                    "action": press.action,
                    "at": press.at
                ]
            }

            call.resolve(["actions": actions])
        }
    }

    @objc func close(_ call: CAPPluginCall) {
        guard let sessionId = call.getString("sessionId") else {
            call.reject("sessionId is required")
            return
        }

        let stepIndex = call.getInt("stepIndex")

        Task { @MainActor in
            await CulinaTimerEngine.shared.close(sessionId: sessionId, stepIndex: stepIndex)
            call.resolve()
        }
    }

    @objc override public func requestPermissions(_ call: CAPPluginCall) {
        Task { @MainActor in
            let granted = await CulinaTimerEngine.shared.requestPermissions()
            call.resolve(["display": granted ? "granted" : "denied"])
        }
    }

    // MARK: NotificationHandlerProtocol

    public func willPresent(notification: UNNotification) -> UNNotificationPresentationOptions {
        CulinaTimerNotifications.presentation(for: notification)
    }

    public func didReceive(response: UNNotificationResponse) {
        Task {
            await CulinaTimerNotifications.receive(response)
        }
    }

    // MARK: Decoding

    /// The page's snapshot, or nil for "no session". Decoded through JSON so the shapes stay those of the shared types.
    private func decode(_ call: CAPPluginCall) -> CulinaTimerSnapshot? {
        guard let sessionId = call.getString("sessionId"),
              let text = call.getObject("text"),
              let textData = try? JSONSerialization.data(withJSONObject: text),
              let timersData = try? JSONSerialization.data(withJSONObject: call.getArray("timers") ?? []),
              let decodedText = try? JSONDecoder().decode(CulinaTimerText.self, from: textData),
              let timers = try? JSONDecoder().decode([CulinaTimer].self, from: timersData) else {
            return nil
        }

        return CulinaTimerSnapshot(sessionId: sessionId, url: call.getString("url") ?? "/", timers: timers, text: decodedText)
    }
}
