using UnityEngine;
using System.IO;   // ⬅️ TAMBAHAN: untuk Path.GetExtension

namespace UnityAndroidOpenUrl
{
    public static class AndroidOpenUrl
    {
        public static void OpenFile(string url, string dataType = null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (string.IsNullOrEmpty(url))
            {
                Debug.LogError("AndroidOpenUrl.OpenFile: url kosong!");
                return;
            }

            // 🔹 Otomatis tentukan MIME TYPE dari ekstensi kalau dataType tidak diisi
            if (string.IsNullOrEmpty(dataType))
            {
                string ext = Path.GetExtension(url).ToLowerInvariant();

                switch (ext)
                {
                    case ".pdf":
                        dataType = "application/pdf";
                        break;

                    case ".doc":
                        dataType = "application/msword";
                        break;

                    case ".docx":
                        dataType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                        break;

                    case ".ppt":
                        dataType = "application/vnd.ms-powerpoint";
                        break;

                    case ".pptx":
                        dataType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                        break;

                    default:
                        dataType = "*/*";   // fallback: biar Android pilih sendiri
                        break;
                }
            }

            // Ambil currentActivity
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            // Class Intent (buat akses ACTION_ dan FLAG_)
            AndroidJavaClass intentClass = new AndroidJavaClass("android.content.Intent");

            // Bikin intent dengan ACTION_VIEW
            AndroidJavaObject intent = new AndroidJavaObject(
                "android.content.Intent",
                intentClass.GetStatic<string>("ACTION_VIEW")
            );

            // Tambah flag: izin baca URI + new task
            intent.Call<AndroidJavaObject>(
                "addFlags",
                intentClass.GetStatic<int>("FLAG_GRANT_READ_URI_PERMISSION")
            );
            intent.Call<AndroidJavaObject>(
                "addFlags",
                intentClass.GetStatic<int>("FLAG_ACTIVITY_NEW_TASK")
            );

            // Cek API level
            int apiLevel = new AndroidJavaClass("android.os.Build$VERSION")
                .GetStatic<int>("SDK_INT");

            AndroidJavaObject uri;

            if (apiLevel >= 24)   // Android 7.0+ → WAJIB FileProvider
            {
                AndroidJavaClass fileProvider =
                    new AndroidJavaClass("androidx.core.content.FileProvider");

                AndroidJavaObject file = new AndroidJavaObject("java.io.File", url);
                AndroidJavaObject unityContext =
                    currentActivity.Call<AndroidJavaObject>("getApplicationContext");

                string packageName = unityContext.Call<string>("getPackageName");
                string authority = packageName + ".fileprovider";

                uri = fileProvider.CallStatic<AndroidJavaObject>(
                    "getUriForFile",
                    unityContext,
                    authority,
                    file
                );
            }
            else
            {
                AndroidJavaClass uriClazz = new AndroidJavaClass("android.net.Uri");
                AndroidJavaObject file = new AndroidJavaObject("java.io.File", url);
                uri = uriClazz.CallStatic<AndroidJavaObject>("fromFile", file);
            }

            // Set data + type sekaligus
            intent.Call<AndroidJavaObject>("setDataAndType", uri, dataType);

            try
            {
                currentActivity.Call("startActivity", intent);
            }
            catch (AndroidJavaException e)
            {
                Debug.LogError("Gagal startActivity: " + e.Message);
            }
#else
            Debug.LogWarning("AndroidOpenUrl.OpenFile hanya jalan di device Android.");
#endif
        }
    }
}
