using UnityEngine;
using System.IO;
using SFB;
public static class TextureLoader
{
    public static bool textureLoaded = false;
    public static Texture2D texture = null;

    public static void LoadTexture()
    {
        var extensions = new[] {
            new ExtensionFilter("Image Files", "png", "jpg", "jpeg"),
        };

        // Open file panel
        string[] paths = StandaloneFileBrowser.OpenFilePanel("Select Image", "", extensions, false);

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
        {
            string selectedPath = paths[0];
            LoadTextureFromFile(selectedPath);
        }
    }

    public static void ClearTexture()
    {
        texture = null;
        textureLoaded = false;
    }

    private static void LoadTextureFromFile(string filePath)
    {
        byte[] imageData = File.ReadAllBytes(filePath);

        //Load Texture
        Texture2D tmpTexture = new Texture2D(2, 2);
        bool isLoaded = tmpTexture.LoadImage(imageData);



        if (isLoaded)
        {
            //Apply Settings to Texture
            tmpTexture.filterMode = FilterMode.Point;
            tmpTexture.wrapMode = TextureWrapMode.Clamp;
            tmpTexture.Apply();

            //Save the Texture
            textureLoaded = true;
            texture = tmpTexture;
        }
        else
        {
            throw new System.Exception("Could not load texture");
        }
    }
}
