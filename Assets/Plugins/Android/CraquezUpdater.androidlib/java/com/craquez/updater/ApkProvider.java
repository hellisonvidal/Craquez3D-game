package com.craquez.updater;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;

import java.io.File;
import java.io.FileNotFoundException;
import java.io.IOException;

/** ContentProvider mínimo e somente-leitura que expõe apenas os arquivos da pasta "updates" do app. */
public class ApkProvider extends ContentProvider {
    public static final String AUTHORITY_SUFFIX = ".craquezapk";
    public static final String APK_MIME = "application/vnd.android.package-archive";

    @Override public boolean onCreate() { return true; }

    private File resolve(Uri uri) throws FileNotFoundException {
        String name = uri.getLastPathSegment();
        if (name == null || name.contains("/") || name.contains("..")) throw new FileNotFoundException("nome inválido");
        File[] roots = new File[] {
            getContext().getExternalFilesDir(null) != null ? new File(getContext().getExternalFilesDir(null), "updates") : null,
            new File(getContext().getFilesDir(), "updates")
        };
        for (File root : roots) {
            if (root == null) continue;
            File f = new File(root, name);
            try {
                if (f.exists() && f.getCanonicalPath().startsWith(root.getCanonicalPath() + File.separator)) return f;
            } catch (IOException ignored) { }
        }
        throw new FileNotFoundException(name);
    }

    @Override
    public ParcelFileDescriptor openFile(Uri uri, String mode) throws FileNotFoundException {
        if (mode != null && mode.contains("w")) throw new SecurityException("somente leitura");
        return ParcelFileDescriptor.open(resolve(uri), ParcelFileDescriptor.MODE_READ_ONLY);
    }

    @Override public String getType(Uri uri) { return APK_MIME; }

    @Override
    public Cursor query(Uri uri, String[] projection, String selection, String[] selectionArgs, String sortOrder) {
        File f;
        try { f = resolve(uri); } catch (FileNotFoundException e) { return null; }
        MatrixCursor c = new MatrixCursor(new String[] { OpenableColumns.DISPLAY_NAME, OpenableColumns.SIZE });
        c.addRow(new Object[] { f.getName(), f.length() });
        return c;
    }

    @Override public Uri insert(Uri uri, ContentValues values) { throw new UnsupportedOperationException(); }
    @Override public int delete(Uri uri, String selection, String[] selectionArgs) { throw new UnsupportedOperationException(); }
    @Override public int update(Uri uri, ContentValues values, String selection, String[] selectionArgs) { throw new UnsupportedOperationException(); }
}
