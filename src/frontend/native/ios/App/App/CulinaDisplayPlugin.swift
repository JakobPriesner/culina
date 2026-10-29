// `CulinaDisplay` for the page (src/lib/app/displaySettings.ts): the display settings WebKit cannot report to a web
// page, so the app's glass turns solid with Reduce Transparency as the system's does. Registered by MainViewController.

import Capacitor
import UIKit

@objc(CulinaDisplayPlugin)
public class CulinaDisplayPlugin: CAPPlugin, CAPBridgedPlugin {
    public let identifier = "CulinaDisplayPlugin"
    public let jsName = "CulinaDisplay"
    public let pluginMethods: [CAPPluginMethod] = [
        CAPPluginMethod(name: "get", returnType: CAPPluginReturnPromise)
    ]

    override public func load() {
        for name in [
            UIAccessibility.reduceTransparencyStatusDidChangeNotification,
            UIAccessibility.darkerSystemColorsStatusDidChangeNotification
        ] {
            NotificationCenter.default.addObserver(self, selector: #selector(changed), name: name, object: nil)
        }
    }

    @objc func get(_ call: CAPPluginCall) {
        DispatchQueue.main.async {
            call.resolve(Self.settings())
        }
    }

    @objc private func changed() {
        notifyListeners("change", data: Self.settings())
    }

    private static func settings() -> [String: Any] {
        [
            "reduceTransparency": UIAccessibility.isReduceTransparencyEnabled,
            "increaseContrast": UIAccessibility.isDarkerSystemColorsEnabled
        ]
    }
}
