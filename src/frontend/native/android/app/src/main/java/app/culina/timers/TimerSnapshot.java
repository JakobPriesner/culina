package app.culina.timers;

import android.content.Context;
import android.content.SharedPreferences;
import java.util.ArrayList;
import java.util.List;
import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

/**
 * What the page sent last (src/lib/features/cooking/nativeTimers.ts), kept so an alarm, a button or a reboot can act with
 * no web view. Also keeps the journal of presses the page applies when it next runs.
 */
final class TimerSnapshot {

    static final class Timer {
        final int stepIndex;
        final String label;
        /** Epoch milliseconds. */
        double endsAt;
        /** Seconds left while paused; null while running. */
        final Double pausedRemaining;

        Timer(int stepIndex, String label, double endsAt, Double pausedRemaining) {
            this.stepIndex = stepIndex;
            this.label = label;
            this.endsAt = endsAt;
            this.pausedRemaining = pausedRemaining;
        }

        boolean isRunning() {
            return pausedRemaining == null;
        }

        boolean isAhead(long now) {
            return isRunning() && endsAt > now;
        }
    }

    final String sessionId;
    final String url;
    final List<Timer> timers;
    /** The words shown, in the app's language: timer, finished, minute1, minute2, dismiss, next, paused, more. */
    final JSONObject text;

    private static final String PREFERENCES = "culina.timers";
    private static final String SNAPSHOT = "snapshot";
    private static final String JOURNAL = "journal";

    TimerSnapshot(String sessionId, String url, List<Timer> timers, JSONObject text) {
        this.sessionId = sessionId;
        this.url = url;
        this.timers = timers;
        this.text = text;
    }

    String text(String key) {
        return text.optString(key, "");
    }

    String label(Timer timer) {
        return timer.label.isEmpty() ? text("timer") : timer.label;
    }

    /** The timer a button was shown for, if it still has that deadline and runs. */
    Timer find(int stepIndex, double endsAt) {
        for (Timer timer : timers) {
            if (timer.stepIndex == stepIndex && timer.endsAt == endsAt && timer.isRunning()) {
                return timer;
            }
        }

        return null;
    }

    /** The running timer that ends first, or null. */
    Timer soonest(long now) {
        Timer soonest = null;

        for (Timer timer : timers) {
            if (timer.isAhead(now) && (soonest == null || timer.endsAt < soonest.endsAt)) {
                soonest = timer;
            }
        }

        return soonest;
    }

    // ── Reading and keeping ────────────────────────────────────────────────

    static TimerSnapshot fromJson(JSONObject json) throws JSONException {
        if (json.isNull("sessionId")) {
            return null;
        }

        JSONArray array = json.optJSONArray("timers");
        List<Timer> timers = new ArrayList<>();

        for (int i = 0; array != null && i < array.length(); i++) {
            JSONObject timer = array.getJSONObject(i);
            timers.add(new Timer(
                timer.getInt("stepIndex"),
                timer.optString("label", ""),
                timer.getDouble("endsAt"),
                timer.has("pausedRemaining") && !timer.isNull("pausedRemaining") ? timer.getDouble("pausedRemaining") : null));
        }

        JSONObject text = json.optJSONObject("text");

        return new TimerSnapshot(json.getString("sessionId"), json.optString("url", "/"), timers, text == null ? new JSONObject() : text);
    }

    JSONObject toJson() throws JSONException {
        JSONArray array = new JSONArray();

        for (Timer timer : timers) {
            JSONObject json = new JSONObject();
            json.put("stepIndex", timer.stepIndex);
            json.put("label", timer.label);
            json.put("endsAt", timer.endsAt);
            if (timer.pausedRemaining != null) {
                json.put("pausedRemaining", timer.pausedRemaining);
            }
            array.put(json);
        }

        return new JSONObject().put("sessionId", sessionId).put("url", url).put("timers", array).put("text", text);
    }

    static TimerSnapshot load(Context context) {
        String stored = preferences(context).getString(SNAPSHOT, null);

        try {
            return stored == null ? null : fromJson(new JSONObject(stored));
        } catch (JSONException unreadable) {
            return null;
        }
    }

    static void store(Context context, TimerSnapshot snapshot) {
        SharedPreferences.Editor editor = preferences(context).edit();

        try {
            if (snapshot == null) {
                editor.remove(SNAPSHOT);
            } else {
                editor.putString(SNAPSHOT, snapshot.toJson().toString());
            }
        } catch (JSONException unwritable) {
            editor.remove(SNAPSHOT);
        }

        editor.apply();
    }

    static void record(Context context, JSONObject press) {
        JSONArray journal = journal(context);
        journal.put(press);
        preferences(context).edit().putString(JOURNAL, journal.toString()).apply();
    }

    /** Hands over and forgets every recorded press. */
    static JSONArray drain(Context context) {
        JSONArray journal = journal(context);
        preferences(context).edit().remove(JOURNAL).apply();

        return journal;
    }

    private static JSONArray journal(Context context) {
        try {
            return new JSONArray(preferences(context).getString(JOURNAL, "[]"));
        } catch (JSONException unreadable) {
            return new JSONArray();
        }
    }

    private static SharedPreferences preferences(Context context) {
        return context.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE);
    }
}
