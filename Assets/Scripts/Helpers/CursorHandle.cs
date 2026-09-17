using UnityEngine;

public class CursorHandle : MonoBehaviour
{
    private void Update()
    {
        Cursor.lockState = _isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !_isLocked;
  
    }
    
    
    public  bool _isLocked;


}
