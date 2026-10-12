import Capacitor
import UIKit

/// The app's web view controller (Main.storyboard): Capacitor's, plus the plugins that are part of this app.
class MainViewController: CAPBridgeViewController {
    override open func capacitorDidLoad() {
        bridge?.registerPluginInstance(CulinaTimersPlugin())
    }
}
