// The app's Live Activities (docs/native-app-design.md, 4.3): one per cooking session for the notification path, and
// AlarmKit's countdown on iOS 26 and later.

import SwiftUI
import WidgetKit

@main
struct CulinaTimerWidgetBundle: WidgetBundle {
    var body: some Widget {
        CulinaTimerLiveActivity()

        if #available(iOS 26.0, *) {
            CulinaAlarmLiveActivity()
        }
    }
}
