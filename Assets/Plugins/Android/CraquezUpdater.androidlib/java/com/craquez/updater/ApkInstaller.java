package com.craquez.updater;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.provider.Settings;
import android.util.Log;

import java.io.File;

/** Abre o instalador do Android para o APK baixado pelo AppUpdater (C#). */
public final class ApkInstaller {
    private ApkInstaller() {}

    /** @return 1 = instalador aberto, 2 = usuário precisa permitir a fonte, 0 = erro. */
    public static int install(Activity activity, String path) {
        try {
            if (Build.VERSION.SDK_INT >= 26 && !activity.getPackageManager().canRequestPackageInstalls()) {
                Intent settings = new Intent(Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                        Uri.parse("package:" + activity.getPackageName()));
                settings.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                activity.startActivity(settings);
                return 2;
            }
            File file = new File(path);
            Uri uri = Uri.parse("content://" + activity.getPackageName() + ApkProvider.AUTHORITY_SUFFIX + "/" + Uri.encode(file.getName()));
            Intent intent = new Intent(Intent.ACTION_VIEW);
            intent.setDataAndType(uri, ApkProvider.APK_MIME);
            intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION | Intent.FLAG_ACTIVITY_NEW_TASK);
            activity.startActivity(intent);
            return 1;
        } catch (Exception e) {
            Log.e("CraquezUpdater", "Falha ao abrir instalador", e);
            return 0;
        }
    }
}
