using UnityEngine;

[ExecuteAlways]
public class CameraFitToBounds : MonoBehaviour
{
    public Camera cam;
    public BoxCollider2D targetBounds; 

    void Reset(){ cam = Camera.main; }
    void LateUpdate(){ if(cam && targetBounds) Fit(); }

    void Fit()
    {
        var b = targetBounds.bounds;
        float aspect = (float)Screen.width / Screen.height;
        float sizeByHeight = b.size.y * 0.5f;
        float sizeByWidth  = (b.size.x * 0.5f) / aspect;
        cam.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth);
        cam.transform.position = new Vector3(b.center.x, b.center.y, cam.transform.position.z);
    }
}
