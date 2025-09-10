using UnityEngine;
public class LookAtCamera : MonoBehaviour
{
    private Camera mainCamera;

    void Start()
    {
        // Get the main camera in the scene
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (mainCamera != null)
        {
            // Make the object look at the camera
            transform.LookAt(mainCamera.transform);

            // Optional: Reverse the forward vector if text is mirrored
            transform.rotation = Quaternion.LookRotation(transform.position - mainCamera.transform.position);
            transform.Rotate(0, 180f, 0);

        }
    }
}
