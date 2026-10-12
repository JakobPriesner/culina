package app.culina.timers;

import android.app.AlarmManager;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.drawable.Icon;
import android.media.AudioAttributes;
import android.media.RingtoneManager;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import app.culina.MainActivity;
import app.culina.R;
import java.util.HashSet;
import java.util.Set;

/**
 * What the system shows and rings for the app's kitchen timers (docs/native-app-design.md, 4.4): one ongoing notification
 * per cooking session whose countdown the system draws (a Live Update on Android 16), and one exact alarm per running
 * timer that posts a sounding notification when it fires. Nothing in the app stays alive for any of it.
 */
final class TimerPresenter {

    static final String ONGOING_CHANNEL = "kitchen-timers";
    static final String ALARM_CHANNEL = "kitchen-alarms";
    static final int ONGOING_ID = 0x0C0011;

    private static final String SCHEDULED = "scheduled";

    private TimerPresenter() {}

    /** Makes the alarms and the ongoing notification exactly `snapshot`'s running timers; null ends everything. */
    static void present(Context context, TimerSnapshot snapshot) {
        createChannels(context, snapshot);
        scheduleAlarms(context, snapshot);
        showOngoing(context, snapshot);
    }

    /** The alarm went off: ring, if the timer still ends then. */
    static void ring(Context context, String sessionId, int stepIndex, double endsAt) {
        TimerSnapshot snapshot = TimerSnapshot.load(context);
        TimerSnapshot.Timer timer = snapshot == null || !snapshot.sessionId.equals(sessionId) ? null : snapshot.find(stepIndex, endsAt);

        if (timer == null) {
            return;
        }

        Notification.Builder builder = builder(context, ALARM_CHANNEL)
            .setSmallIcon(R.drawable.ic_stat_culina_timer)
            .setContentTitle(snapshot.label(timer))
            .setContentText(snapshot.text("finished"))
            .setCategory(Notification.CATEGORY_ALARM)
            .setAutoCancel(true)
            .setContentIntent(open(context, snapshot, timer, "open"))
            .addAction(action(context, snapshot, timer, "minute-1", snapshot.text("minute1")))
            .addAction(action(context, snapshot, timer, "minute-2", snapshot.text("minute2")))
            .addAction(action(context, snapshot, timer, "dismiss", snapshot.text("dismiss")));

        // Before channels: the priority and sound are the notification's own.
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) {
            legacyAlarm(builder);
        }

        manager(context).notify(alertId(sessionId, stepIndex), builder.build());

        // The finished timer leaves the countdown; the next one takes its place.
        showOngoing(context, snapshot);
    }

    /** Takes finished timers' notifications off the screen: one step's, or every one of the session's. */
    static void close(Context context, String sessionId, Integer stepIndex) {
        TimerSnapshot snapshot = TimerSnapshot.load(context);

        if (stepIndex != null) {
            manager(context).cancel(alertId(sessionId, stepIndex));
            return;
        }

        if (snapshot != null && snapshot.sessionId.equals(sessionId)) {
            for (TimerSnapshot.Timer timer : snapshot.timers) {
                manager(context).cancel(alertId(sessionId, timer.stepIndex));
            }
        }
    }

    static boolean canRingExactly(Context context) {
        return Build.VERSION.SDK_INT < Build.VERSION_CODES.S || alarms(context).canScheduleExactAlarms();
    }

    // ── Alarms ─────────────────────────────────────────────────────────────

    private static void scheduleAlarms(Context context, TimerSnapshot snapshot) {
        SharedPreferences preferences = context.getSharedPreferences("culina.timers", Context.MODE_PRIVATE);
        Set<String> previous = preferences.getStringSet(SCHEDULED, new HashSet<>());
        Set<String> now = new HashSet<>();
        long time = System.currentTimeMillis();

        if (snapshot != null) {
            for (TimerSnapshot.Timer timer : snapshot.timers) {
                if (!timer.isAhead(time)) {
                    continue;
                }

                PendingIntent ring = ringIntent(context, snapshot.sessionId, timer.stepIndex, timer.endsAt);
                long at = (long) timer.endsAt;

                // Without the exact-alarm permission (denied by default for new installs on Android 14) Doze may hold
                // an alarm back by minutes; the app asks for it when the first timer starts.
                if (canRingExactly(context)) {
                    alarms(context).setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, at, ring);
                } else {
                    alarms(context).setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, at, ring);
                }

                now.add(key(snapshot.sessionId, timer.stepIndex));
            }
        }

        for (String stale : previous) {
            if (!now.contains(stale)) {
                String[] parts = stale.split("\n", 2);
                PendingIntent ring = ringIntent(context, parts[0], Integer.parseInt(parts[1]), 0);
                alarms(context).cancel(ring);
                ring.cancel();
            }
        }

        preferences.edit().putStringSet(SCHEDULED, now).apply();
    }

    /** One pending intent per (session, step): the data URI makes them distinct, the extras carry the deadline. */
    private static PendingIntent ringIntent(Context context, String sessionId, int stepIndex, double endsAt) {
        Intent intent = new Intent(context, TimerReceiver.class)
            .setAction(TimerReceiver.ACTION_RING)
            .setData(Uri.parse("culina-timer://" + Uri.encode(sessionId) + "/" + stepIndex))
            .putExtra(TimerReceiver.EXTRA_SESSION, sessionId)
            .putExtra(TimerReceiver.EXTRA_STEP, stepIndex)
            .putExtra(TimerReceiver.EXTRA_ENDS_AT, endsAt);

        return PendingIntent.getBroadcast(
            context, 0, intent, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }

    // ── The ongoing countdown ──────────────────────────────────────────────

    private static void showOngoing(Context context, TimerSnapshot snapshot) {
        long now = System.currentTimeMillis();
        TimerSnapshot.Timer soonest = snapshot == null ? null : snapshot.soonest(now);

        if (soonest == null) {
            manager(context).cancel(ONGOING_ID);
            return;
        }

        int others = -1;
        for (TimerSnapshot.Timer timer : snapshot.timers) {
            if (timer.isAhead(now) || !timer.isRunning()) {
                others++;
            }
        }

        Notification.Builder builder = builder(context, ONGOING_CHANNEL)
            .setSmallIcon(R.drawable.ic_stat_culina_timer)
            .setContentTitle(snapshot.label(soonest))
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setShowWhen(true)
            .setWhen((long) soonest.endsAt)
            .setUsesChronometer(true)
            .setChronometerCountDown(true)
            .setCategory(Notification.CATEGORY_PROGRESS)
            .setContentIntent(open(context, snapshot, soonest, "open"))
            .addAction(action(context, snapshot, soonest, "minute-1", snapshot.text("minute1")))
            .addAction(action(context, snapshot, soonest, "dismiss", snapshot.text("dismiss")))
            .addAction(next(context, snapshot, soonest));

        if (others > 0) {
            builder.setContentText(snapshot.text("more").replace("{count}", String.valueOf(others)));
        }

        // Android 16: promoted to a Live Update, a chip in the status bar. Shown as an ordinary ongoing notification
        // where the person turned that off or the system declines.
        // The request is an extra in API 36 (Builder.setRequestPromotedOngoing came later), as AndroidX's
        // NotificationCompat sets it.
        if (Build.VERSION.SDK_INT >= 36) {
            Bundle promoted = new Bundle();
            promoted.putBoolean("android.requestPromotedOngoing", true);
            builder.addExtras(promoted);
            builder.setShortCriticalText(snapshot.label(soonest));
        }

        manager(context).notify(ONGOING_ID, builder.build());
    }

    // ── Buttons ────────────────────────────────────────────────────────────

    /** A button applied by TimerReceiver without opening the app. */
    private static Notification.Action action(
        Context context, TimerSnapshot snapshot, TimerSnapshot.Timer timer, String action, String title) {
        Intent intent = new Intent(context, TimerReceiver.class)
            .setAction(TimerReceiver.ACTION_PRESS)
            .setData(Uri.parse("culina-timer://" + Uri.encode(snapshot.sessionId) + "/" + timer.stepIndex + "/" + action))
            .putExtra(TimerReceiver.EXTRA_SESSION, snapshot.sessionId)
            .putExtra(TimerReceiver.EXTRA_STEP, timer.stepIndex)
            .putExtra(TimerReceiver.EXTRA_ENDS_AT, timer.endsAt)
            .putExtra(TimerReceiver.EXTRA_ACTION, action);

        PendingIntent pending = PendingIntent.getBroadcast(
            context, 0, intent, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);

        return new Notification.Action.Builder(
            Icon.createWithResource(context, R.drawable.ic_stat_culina_timer), title, pending).build();
    }

    /** "Next step" opens the app on the recipe: a receiver may not start an activity from the background. */
    private static Notification.Action next(Context context, TimerSnapshot snapshot, TimerSnapshot.Timer timer) {
        return new Notification.Action.Builder(
            Icon.createWithResource(context, R.drawable.ic_stat_culina_timer),
            snapshot.text("next"),
            open(context, snapshot, timer, "next")).build();
    }

    private static PendingIntent open(Context context, TimerSnapshot snapshot, TimerSnapshot.Timer timer, String action) {
        Intent intent = new Intent(context, MainActivity.class)
            .setAction(TimerReceiver.ACTION_PRESS)
            .setData(Uri.parse("culina-timer://" + Uri.encode(snapshot.sessionId) + "/" + timer.stepIndex + "/" + action))
            .addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP | Intent.FLAG_ACTIVITY_CLEAR_TOP)
            .putExtra(TimerReceiver.EXTRA_SESSION, snapshot.sessionId)
            .putExtra(TimerReceiver.EXTRA_STEP, timer.stepIndex)
            .putExtra(TimerReceiver.EXTRA_ENDS_AT, timer.endsAt)
            .putExtra(TimerReceiver.EXTRA_ACTION, action);

        return PendingIntent.getActivity(
            context, 0, intent, PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }

    // ── Plumbing ───────────────────────────────────────────────────────────

    private static void createChannels(Context context, TimerSnapshot snapshot) {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O || snapshot == null) {
            return;
        }

        NotificationChannel ongoing = new NotificationChannel(
            ONGOING_CHANNEL, snapshot.text("timer"), NotificationManager.IMPORTANCE_LOW);
        ongoing.setShowBadge(false);
        ongoing.setSound(null, null);

        NotificationChannel alarm = new NotificationChannel(
            ALARM_CHANNEL, snapshot.text("finished"), NotificationManager.IMPORTANCE_HIGH);
        alarm.enableVibration(true);
        alarm.setVibrationPattern(new long[] {0, 250, 100, 250, 100, 500});
        alarm.setSound(
            RingtoneManager.getDefaultUri(RingtoneManager.TYPE_ALARM),
            new AudioAttributes.Builder().setUsage(AudioAttributes.USAGE_ALARM).build());

        manager(context).createNotificationChannel(ongoing);
        manager(context).createNotificationChannel(alarm);
    }

    @SuppressWarnings("deprecation")
    private static void legacyAlarm(Notification.Builder builder) {
        builder.setPriority(Notification.PRIORITY_MAX).setDefaults(Notification.DEFAULT_ALL);
    }

    @SuppressWarnings("deprecation")
    private static Notification.Builder builder(Context context, String channel) {
        return Build.VERSION.SDK_INT >= Build.VERSION_CODES.O
            ? new Notification.Builder(context, channel)
            : new Notification.Builder(context);
    }

    static int alertId(String sessionId, int stepIndex) {
        return (sessionId + ":" + stepIndex).hashCode() & 0x7fffffff;
    }

    private static String key(String sessionId, int stepIndex) {
        return sessionId + "\n" + stepIndex;
    }

    private static NotificationManager manager(Context context) {
        return (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);
    }

    private static AlarmManager alarms(Context context) {
        return (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
    }
}
