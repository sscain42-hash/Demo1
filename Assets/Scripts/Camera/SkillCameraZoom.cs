using UnityEngine;
using Unity.Cinemachine;
using NaughtyAttributes; // Nhớ import Cinemachine

public class SkillCameraZoom : MonoBehaviour
{
    public static SkillCameraZoom Instance { get; private set; }
    [SerializeField] private CinemachineCamera zoomVCam;
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 0;

    private void Start()
    {
        Instance = this;
    }
    // Gọi hàm này khi BẮT ĐẦU gồng chiêu / xuất đòn
    [Button("Zoom In")]
    public void ZoomIn()
    {
        if (zoomVCam != null)
        {
         
            zoomVCam.Priority = activePriority; // Đẩy Priority lên cao -> Cinemachine tự blend Zoom vào
        }
        Debug.Log("Zoom In");
    }

    // Gọi hàm này khi ĐÁNH XONG / Kết thúc đòn
    [Button("Zoom Out")]
    public void ZoomOut()
    {
        if (zoomVCam != null)
        {
            zoomVCam.Priority = inactivePriority; // Hạ Priority xuống -> Cinemachine tự blend trả cam về cũ
        }
    }
}