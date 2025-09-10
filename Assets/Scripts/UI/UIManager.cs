using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class UIManager : MonoBehaviour
{

    public GameObject buttonLoadImage;
    public GameObject buttonChangeColor;
    public GameObject sliderPanel;
    public GameObject sliderObject;
    public GameObject imageObject;
    public ShaderManager shaderManager;

    public GameObject RGBAxis;
    public GameObject HSVAxis;


    private bool imageLoaded = false;


    // Start is called before the first frame update
    void Start()
    {

        //Disable UI Elements that are not usable without a loaded imageObject
        HideUI();

    }

    // Update is called once per frame
    void Update()
    {

    }



    /// <summary>
    /// Hides all UI Elements that are not usable without a loaded imageObject
    /// </summary>
    private void HideUI()
    {
        buttonChangeColor.SetActive(false);
        sliderPanel.SetActive(false);
        imageObject.SetActive(false);

        //Disable both Coordinate axis
        ToggleCoordinateAxis();
    }


    /// <summary>
    /// Displayes all the UI when an imageObject is loaded
    /// </summary>
    private void ShowUI()
    {
        buttonChangeColor.SetActive(true);
        sliderPanel.SetActive(true);
        imageObject.SetActive(true);

        //Enable the active Coordinate System
        ToggleCoordinateAxis();

        //Set the buttons text based on the Color Space
        ChangeColorSpaceButton();


        //Match the sliderObject value to the value of Shadermanager
        UnityEngine.UI.Slider slider = sliderObject.GetComponent<UnityEngine.UI.Slider>();
        float sliderValue = shaderManager.GetScale();
        slider.value = sliderValue;


        //Set the loaded Texture as the texture for the imageObject        
        Texture2D texture = shaderManager.GetImageTexture();
        UnityEngine.UI.Image image = imageObject.GetComponent<UnityEngine.UI.Image>();
        if (texture != null)
        {
            Sprite newSprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f) // center pivot
            );

            image.sprite = newSprite;
        }
    }




    /// <summary>
    /// Method Called by button press of buttonLoadImage
    /// </summary>
    public void LoadImage()
    {
        //Reset flag
        imageLoaded = false;

        //Hide unusabel UI
        HideUI();

        //Load imageObject on the GPU
        shaderManager.LoadImage();

        //Check if imageObject load was succsesfull
        imageLoaded = shaderManager.GetImageLoaded();

        //When imageObject is loaded succesfully show other UI elements
        if (imageLoaded)
        {
            ShowUI();
        }
    }


    /// <summary>
    /// Method Called by button press of buttonChangeColor
    /// </summary>
    public void ChangeColorSpace()
    {
        //Check if shadermanager is ready for an animation
        if (shaderManager.ReadyForAnimation() == false)
        {
            return;
        }

        //Start the animation
        shaderManager.StartAnimation();

        //Toggle which axis is active
        ToggleCoordinateAxis();

        //Change the buttons text
        ChangeColorSpaceButton();
    }


    /// <summary>
    /// Method Called by value Changed in Slider
    /// </summary>
    public void ScaleChanged()
    {
        //Get the slider Value
        UnityEngine.UI.Slider slider = sliderObject.GetComponent<UnityEngine.UI.Slider>();
        float scale = slider.value;

        //Set the value for the shader manager
        shaderManager.SetScale(scale);
    }



    /// <summary>
    /// Toggles which Coordinate System is visible
    /// </summary>
    private void ToggleCoordinateAxis()
    {
        //Read back what colorspace is active
        COLOR_SPACE colorSpace = shaderManager.GetColorSpace();

        //No Coordinate System is active
        if (imageLoaded == false)
        {
            RGBAxis.SetActive(false);
            HSVAxis.SetActive(false);
            return;
        }

        switch (colorSpace)
        {
            case COLOR_SPACE.RGB:
                RGBAxis.SetActive(true);
                HSVAxis.SetActive(false);
                break;
            case COLOR_SPACE.HSV:
                RGBAxis.SetActive(false);
                HSVAxis.SetActive(true);
                break;
        }
    }


    /// <summary>
    /// Changes the Buttons Text based on the color space
    /// </summary>
    private void ChangeColorSpaceButton()
    {

        //Get the text Fields of the buttons
        UnityEngine.UI.Button button = buttonChangeColor.GetComponent<UnityEngine.UI.Button>();
        TextMeshProUGUI textField = button.GetComponentInChildren<TextMeshProUGUI>();


        //Read back what colorspace is active
        COLOR_SPACE colorSpace = shaderManager.GetColorSpace();

        //Chane the text based on the color Space
        switch (colorSpace)
        {
            case COLOR_SPACE.RGB:
                textField.text = "CHANGE COLORSPACE TO HSV";
                break;
            case COLOR_SPACE.HSV:
                textField.text = "CHANGE COLORSPACE TO RGB";
                break;
        }
    }
}
