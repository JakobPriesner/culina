import ActivityKit
import AlarmKit
import SwiftUI
import WidgetKit

/// AlarmKit's countdown for one timer (iOS 26 and later). The system rings it, through the silent switch and Focus; this
/// only draws the time left, which the system ticks.
@available(iOS 26.0, *)
struct CulinaAlarmLiveActivity: Widget {
    var body: some WidgetConfiguration {
        ActivityConfiguration(for: AlarmAttributes<CulinaAlarmMetadata>.self) { context in
            HStack {
                Text(label(context.attributes)).font(.headline).lineLimit(1)
                Spacer()
                AlarmTime(state: context.state).font(.title2.monospacedDigit())
            }
            .padding()
            .activityBackgroundTint(Color.black.opacity(0.6))
        } dynamicIsland: { context in
            DynamicIsland {
                DynamicIslandExpandedRegion(.leading) {
                    Image(systemName: "timer").foregroundStyle(.orange)
                }
                DynamicIslandExpandedRegion(.center) {
                    Text(label(context.attributes)).lineLimit(1)
                }
                DynamicIslandExpandedRegion(.trailing) {
                    AlarmTime(state: context.state).font(.title3.monospacedDigit())
                }
            } compactLeading: {
                Image(systemName: "timer").foregroundStyle(.orange)
            } compactTrailing: {
                AlarmTime(state: context.state).monospacedDigit().frame(maxWidth: 56)
            } minimal: {
                Image(systemName: "timer").foregroundStyle(.orange)
            }
        }
    }

    private func label(_ attributes: AlarmAttributes<CulinaAlarmMetadata>) -> String {
        attributes.metadata?.label ?? ""
    }
}

@available(iOS 26.0, *)
private struct AlarmTime: View {
    let state: AlarmPresentationState

    var body: some View {
        switch state.mode {
        case .countdown(let countdown):
            Text(timerInterval: Date.now...max(Date.now, countdown.fireDate), countsDown: true)
        case .paused(let paused):
            let left = paused.totalCountdownDuration - paused.previouslyElapsedDuration
            Text(String(format: "%d:%02d", Int(left) / 60, Int(left) % 60))
        default:
            Image(systemName: "bell.fill")
        }
    }
}
