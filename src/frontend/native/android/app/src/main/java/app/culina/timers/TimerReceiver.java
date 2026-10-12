package app.culina.timers;

import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import org.json.JSONException;
import org.json.JSONObject;

/**
 * An alarm going off, or a button pressed on a timer notification, with or without the app running. A press is applied
 * here at once (the same rules as the website's `applyTimerAction`: only a timer that still has the deadline the button
 * was shown for) and recorded for the page, which applies it again when it next runs and so lands on the same state.
 */
public final class TimerReceiver extends BroadcastReceiver {

    static final String ACTION_RING = "app.culina.timers.RING";
    static final String ACTION_PRESS = "app.culina.timers.PRESS";
    static final String EXTRA_SESSION = "sessionId";
    static final String EXTRA_STEP = "stepIndex";
    static final String EXTRA_ENDS_AT = "endsAt";
    static final String EXTRA_ACTION = "action";

    /** Set by the plugin while the page is alive, so it drains a press at once. */
    static volatile Runnable onPress;

    @Override
    public void onReceive(Context context, Intent intent) {
        String sessionId = intent.getStringExtra(EXTRA_SESSION);

        if (sessionId == null) {
            return;
        }

        int stepIndex = intent.getIntExtra(EXTRA_STEP, -1);
        double endsAt = intent.getDoubleExtra(EXTRA_ENDS_AT, 0);

        if (ACTION_RING.equals(intent.getAction())) {
            TimerPresenter.ring(context, sessionId, stepIndex, endsAt);
        } else if (ACTION_PRESS.equals(intent.getAction())) {
            press(context, sessionId, stepIndex, endsAt, intent.getStringExtra(EXTRA_ACTION));
        }
    }

    /** Applies and records one press; also used by MainActivity for the buttons that open the app. */
    static void press(Context context, String sessionId, int stepIndex, double endsAt, String action) {
        TimerSnapshot snapshot = TimerSnapshot.load(context);

        if (snapshot == null || !snapshot.sessionId.equals(sessionId) || action == null) {
            return;
        }

        long at = System.currentTimeMillis();
        TimerSnapshot.Timer timer = snapshot.find(stepIndex, endsAt);

        if (timer != null) {
            switch (action) {
                case "minute-1":
                    timer.endsAt = at + 60_000;
                    break;
                case "minute-2":
                    timer.endsAt = at + 120_000;
                    break;
                case "dismiss":
                case "next":
                    snapshot.timers.remove(timer);
                    break;
                default:
                    break;
            }
        }

        try {
            TimerSnapshot.record(
                context,
                new JSONObject()
                    .put("sessionId", sessionId)
                    .put("stepIndex", stepIndex)
                    .put("endsAt", endsAt)
                    .put("url", snapshot.url)
                    .put("action", action)
                    .put("at", at));
        } catch (JSONException unwritable) {
            // The system's state still changed; the page will catch up from its own copy.
        }

        TimerSnapshot.store(context, snapshot);
        TimerPresenter.present(context, snapshot);
        TimerPresenter.close(context, sessionId, stepIndex);

        Runnable listener = onPress;
        if (listener != null) {
            listener.run();
        }
    }
}
