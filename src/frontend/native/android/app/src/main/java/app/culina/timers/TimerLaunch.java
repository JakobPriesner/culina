package app.culina.timers;

import android.content.Context;
import android.content.Intent;

/** A timer notification or its "Next step" opened the app: recorded like any press, for the page to open the recipe. */
public final class TimerLaunch {

    private TimerLaunch() {}

    public static void handle(Context context, Intent intent) {
        if (intent == null || !TimerReceiver.ACTION_PRESS.equals(intent.getAction())) {
            return;
        }

        String sessionId = intent.getStringExtra(TimerReceiver.EXTRA_SESSION);

        if (sessionId != null) {
            TimerReceiver.press(
                context,
                sessionId,
                intent.getIntExtra(TimerReceiver.EXTRA_STEP, -1),
                intent.getDoubleExtra(TimerReceiver.EXTRA_ENDS_AT, 0),
                intent.getStringExtra(TimerReceiver.EXTRA_ACTION));
        }

        // Handled once: a configuration change re-delivers the same intent.
        intent.setAction(null);
    }
}
