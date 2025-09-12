using System;
using UnityEngine;
using System.Runtime.InteropServices;

public class TextureUploadHandler : MonoBehaviour
{
#if UNITY_WEBGL
    [DllImport("__Internal")]
    private static extern void UploadFile(string gameObjectName, string methodName);
#endif

    public void TriggerUpload()
    {
#if UNITY_WEBGL
        UploadFile(gameObject.name, "OnFileUploaded");
#else
        Debug.LogWarning("TriggerUpload should only be called on WebGL builds.");
#endif
    }

    public void OnFileUploaded(string base64Data)
    {
        byte[] imageBytes = Convert.FromBase64String(base64Data);
        Texture2D tmpTexture = new Texture2D(2, 2);

        if (tmpTexture.LoadImage(imageBytes))
        {
            tmpTexture.filterMode = FilterMode.Point;
            tmpTexture.wrapMode = TextureWrapMode.Clamp;
            tmpTexture.Apply();

            // Pass the loaded texture back to the static TextureLoader
            TextureLoader.OnWebGLTextureReceived(tmpTexture);
        }
        else
        {
            Debug.LogError("Failed to load image from uploaded file.");
        }
    }
}
