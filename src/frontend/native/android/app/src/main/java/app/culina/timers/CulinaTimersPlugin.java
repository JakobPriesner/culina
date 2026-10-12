package app.culina.timers;

import android.Manifest;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;
import com.getcapacitor.JSArray;
import com.getcapacitor.JSObject;
import com.getcapacitor.PermissionState;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;
import com.getcapacitor.annotation.Permission;
import com.getcapacitor.annotation.PermissionCallback;
import org.json.JSONException;

/** `CulinaTimers` for the page (src/lib/features/cooking/nativeTimers.ts); registered by MainActivity. */
@CapacitorPlugin(
    name = "CulinaTimers",
    permissions = @Permission(strings = {Manifest.permission.POST_NOTIFICATIONS}, alias = "display"))
public class CulinaTimersPlugin extends Plugin {

    private static final String ASKED_EXACT = "askedExactAlarms";

    @Override
    public void load() {
        TimerReceiver.onPress = () -> notifyListeners("action", new JSObject());
    }

    @Override
    protected void handleOnDestroy() {
        TimerReceiver.onPress = null;
    }

    @PluginMethod
    public void sync(PluginCall call) {
        try {
            TimerSnapshot snapshot = TimerSnapshot.fromJson(call.getData());
            TimerSnapshot.store(getContext(), snapshot);
            TimerPresenter.present(getContext(), snapshot);
            call.resolve();
        } catch (JSONException unreadable) {
            call.reject("Unreadable timers", unreadable);
        }
    }

    @PluginMethod
    public void drain(PluginCall call) {
        JSObject result = new JSObject();

        try {
            result.put("actions", new JSArray(TimerSnapshot.drain(getContext()).toString()));
        } catch (JSONException unreadable) {
            result.put("actions", new JSArray());
        }

        call.resolve(result);
    }

    @PluginMethod
    public void close(PluginCall call) {
        String sessionId = call.getString("sessionId");

        if (sessionId == null) {
            call.reject("sessionId is required");
            return;
        }

        TimerPresenter.close(getContext(), sessionId, call.getInt("stepIndex"));
        call.resolve();
    }

    @PluginMethod
    @Override
    public void requestPermissions(PluginCall call) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU && getPermissionState("display") != PermissionState.GRANTED) {
            requestPermissionForAlias("display", call, "displayAnswered");
        } else {
            displayAnswered(call);
        }
    }

    @PermissionCallback
    private void displayAnswered(PluginCall call) {
        boolean granted = Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU
            || getPermissionState("display") == PermissionState.GRANTED;

        askForExactAlarmsOnce();

        JSObject result = new JSObject();
        result.put("display", granted ? "granted" : "denied");
        call.resolve(result);
    }

    /**
     * Exact alarms are off by default for new installs on Android 14; without them a timer can ring minutes late in Doze.
     * The system's settings page is the only place to allow it, so it is opened once, the first time a timer starts.
     */
    private void askForExactAlarmsOnce() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.S || TimerPresenter.canRingExactly(getContext())) {
            return;
        }

        android.content.SharedPreferences preferences =
            getContext().getSharedPreferences("culina.timers", android.content.Context.MODE_PRIVATE);

        if (preferences.getBoolean(ASKED_EXACT, false)) {
            return;
        }

        preferences.edit().putBoolean(ASKED_EXACT, true).apply();
        getActivity().startActivity(
            new Intent(Settings.ACTION_REQUEST_SCHEDULE_EXACT_ALARM, Uri.parse("package:" + getContext().getPackageName())));
    }
}
