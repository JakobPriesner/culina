package app.culina;

import android.content.Intent;
import android.os.Bundle;
import app.culina.timers.CulinaTimersPlugin;
import app.culina.timers.TimerLaunch;
import com.getcapacitor.BridgeActivity;

public class MainActivity extends BridgeActivity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        // Part of this app, not an npm package, so registered by hand (docs/native-app-design.md, 4.4).
        registerPlugin(CulinaTimersPlugin.class);
        super.onCreate(savedInstanceState);
        TimerLaunch.handle(this, getIntent());
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        TimerLaunch.handle(this, intent);
    }
}
