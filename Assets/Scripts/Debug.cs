using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Debug : MonoBehaviour
{
    public Texture2D texture;
    // Start is called before the first frame update
    void Start()
    {

    }

    void Update()
    {
        if (Input.GetButtonDown("Jump"))
        {
            if (TextureLoader.textureLoaded == false)
            {
                TextureLoader.LoadTexture();
            }
            else
            {
                TextureLoader.ClearTexture();
            }

            texture = TextureLoader.texture;

        }

    }
}
