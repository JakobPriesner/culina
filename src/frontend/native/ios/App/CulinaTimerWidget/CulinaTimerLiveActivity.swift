import ActivityKit
import AppIntents
import SwiftUI
import WidgetKit

/// A cooking session's timers on the Lock Screen and in the Dynamic Island. The system ticks every countdown from its
/// deadline (`Text(timerInterval:)`), so the app never updates this per second and the web view can sleep.
struct CulinaTimerLiveActivity: Widget {
    var body: some WidgetConfiguration {
        ActivityConfiguration(for: CulinaTimerAttributes.self) { context in
            LockScreenTimers(sessionId: context.attributes.sessionId, state: context.state, stale: context.isStale)
                .padding()
                .activityBackgroundTint(Color.black.opacity(0.6))
                .activitySystemActionForegroundColor(.orange)
        } dynamicIsland: { context in
            let soonest = context.state.timers.first
            let text = context.state.text

            return DynamicIsland {
                DynamicIslandExpandedRegion(.leading) {
                    Image(systemName: "timer").foregroundStyle(.orange)
                }
                DynamicIslandExpandedRegion(.trailing) {
                    if let soonest {
                        Countdown(timer: soonest, text: text, stale: context.isStale)
                            .font(.title3.monospacedDigit())
                    }
                }
                DynamicIslandExpandedRegion(.center) {
                    if let soonest {
                        Text(soonest.label.isEmpty ? text.timer : soonest.label).lineLimit(1)
                    }
                }
                DynamicIslandExpandedRegion(.bottom) {
                    VStack(spacing: 6) {
                        if let soonest {
                            TimerButtons(sessionId: context.attributes.sessionId, timer: soonest, text: text)
                        }
                        if context.state.timers.count > 1 {
                            Text(text.more(context.state.timers.count - 1))
                                .font(.caption)
                                .foregroundStyle(.secondary)
                        }
                    }
                }
            } compactLeading: {
                Image(systemName: "timer").foregroundStyle(.orange)
            } compactTrailing: {
                if let soonest {
                    Countdown(timer: soonest, text: text, stale: context.isStale)
                        .monospacedDigit()
                        .frame(maxWidth: 56)
                }
            } minimal: {
                if let soonest, soonest.isRunning, soonest.deadline > .now {
                    ProgressView(
                        timerInterval: Date.now...soonest.deadline,
                        countsDown: true,
                        label: { EmptyView() },
                        currentValueLabel: { Image(systemName: "timer") })
                        .progressViewStyle(.circular)
                        .tint(.orange)
                } else {
                    Image(systemName: "timer").foregroundStyle(.orange)
                }
            }
        }
    }
}

/// Up to three timers, the soonest first, with buttons for the soonest.
private struct LockScreenTimers: View {
    let sessionId: String
    let state: CulinaTimerAttributes.ContentState
    let stale: Bool

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            ForEach(state.timers.prefix(3), id: \.stepIndex) { timer in
                HStack {
                    Text(timer.label.isEmpty ? state.text.timer : timer.label)
                        .font(.headline)
                        .lineLimit(1)
                    Spacer()
                    Countdown(timer: timer, text: state.text, stale: stale && timer == state.timers.first)
                        .font(.title2.monospacedDigit())
                }
            }

            if state.timers.count > 3 {
                Text(state.text.more(state.timers.count - 3))
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            if let soonest = state.timers.first {
                TimerButtons(sessionId: sessionId, timer: soonest, text: state.text)
            }
        }
    }
}

/// A timer's time: counting down, paused, or done.
private struct Countdown: View {
    let timer: CulinaTimer
    let text: CulinaTimerText
    let stale: Bool

    var body: some View {
        if let paused = timer.pausedRemaining {
            Text("\(text.paused) \(Self.minutes(paused))")
        } else if !stale, timer.deadline > .now {
            Text(timerInterval: Date.now...timer.deadline, countsDown: true)
                .multilineTextAlignment(.trailing)
        } else {
            Text(text.finished)
        }
    }

    static func minutes(_ seconds: Double) -> String {
        let whole = Int(seconds.rounded(.up))
        return String(format: "%d:%02d", whole / 60, whole % 60)
    }
}

/// +1 min and Dismiss for a running timer: applied in the app's process without opening it (CulinaTimerButtonIntent).
private struct TimerButtons: View {
    let sessionId: String
    let timer: CulinaTimer
    let text: CulinaTimerText

    var body: some View {
        if #available(iOS 17.0, *), timer.isRunning {
            HStack {
                Button(intent: CulinaTimerButtonIntent(
                    sessionId: sessionId, stepIndex: timer.stepIndex, endsAt: timer.endsAt, action: "minute-1")) {
                    Text(text.minute1)
                }
                Button(intent: CulinaTimerButtonIntent(
                    sessionId: sessionId, stepIndex: timer.stepIndex, endsAt: timer.endsAt, action: "dismiss")) {
                    Text(text.dismiss)
                }
            }
            .buttonStyle(.bordered)
            .tint(.orange)
        }
    }
}
