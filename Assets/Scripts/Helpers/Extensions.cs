using UnityEngine;


public static class Extensions 
{
    #region LayerMask
    /// <summary>
    /// Trả về True nếu gameObject có cùng LayerMask
    /// </summary>
    /// <param name="gameObject"> Object cần kiểm tra Layer </param>
    /// <returns></returns>
    public static bool Contains(this LayerMask layers, GameObject gameObject) => 0 != (layers.value & 1 << gameObject.layer);

    /// <summary>
    /// Set Layer cho Object theo LayerMask truyền vào, nếu LayerMask có từ 2 bit trở lên sẽ trả về bit đầu tiên (tức là LayerMask đầu tiên)
    /// </summary>
    /// <param name="_layerMask"> Layer cần set vào Object. </param>
    /// <returns></returns>
    public static void SetObjectLayer(this GameObject _gameObject, LayerMask _layerMask) => _gameObject.layer = GetObjectLayerIndex(_layerMask);
    
    /// <summary>
    /// Trả về index của LayerMask truyền vào, nếu LayerMask có từ 2 bit trở lên sẽ trả về bit đầu tiên (tức là LayerMask đầu tiên)
    /// </summary>
    /// <param name="_layerMask"> Layer cần lấy Index. </param>
    /// <returns></returns>
    public static int GetObjectLayerIndex(LayerMask _layerMask)
    {
        if (_layerMask.value == 0) return 0;
        for (var i = 0; i < 32; i++)
        {
            var _layerValue = 1 << i;
            if ((_layerMask.value & _layerValue) != 0) return i;
        }
        return 0;
    }
    #endregion
    
    
    #region AnimatorController
    /// <summary>
    /// Set 1 clip mới vào bộ animation controller(Override)
    /// </summary>
    /// <param name="_slotName"> Tên vị trí cần set </param>
    /// <param name="_clip"> Clip cần set </param>
    public static void SetClip(this AnimatorOverrideController _overrideController, string _slotName, AnimationClip _clip)
        => _overrideController[_slotName] = _clip;
    
    /// <summary>
    /// Trả về độ dài của clip
    /// </summary>
    /// <param name="_slotName"> Clip cần lấy độ dài </param>
    /// <returns></returns>
    public static float Length(this AnimatorOverrideController _overrideController, string _slotName)
        => _overrideController[_slotName].length;


    /// <summary>
    /// Trả về độ dài của animationClip hiện tại trên Layer được chỉ định
    /// </summary>
    /// <param name="layerIndex"> Layer cần lấy độ dài của animationClip </param>
    /// <returns></returns>
    public static float Length(this Animator _animator, int layerIndex)
        => _animator.GetCurrentAnimatorStateInfo(layerIndex).length;
    
    /// <summary>
    /// Trả về True nếu Tag của animation hiện tại đang chạy giống với Tag của paramater
    /// </summary>
    /// <param name="tagCheck"> Tag cần kiểm tra </param>
    /// <returns></returns>
    public static bool IsTag(this Animator _animator, string tagCheck)
        => _animator.GetCurrentAnimatorStateInfo(0).IsTag(tagCheck);
    
    /// <summary>
    /// Trả về True nếu Tag của animation hiện tại đang chạy giống với Tag của paramater
    /// </summary>
    /// <param name="layerIndex"> Layer cần kiểm tra </param>
    /// <param name="tagCheck"> Tag cần kiểm tra </param>
    /// <returns></returns>
    public static bool IsTag(this Animator _animator, string tagCheck, int layerIndex)
        => _animator.GetCurrentAnimatorStateInfo(layerIndex).IsTag(tagCheck);
    #endregion


    #region Spine
    /// <summary>
    /// Set Spine Animation theo tên truyền vào.
    /// </summary>
    /// <param name="animationName"> Tên animation cần thao tác </param>
    /// <param name="isLoop"> Có lặp lại hay không ? </param>
    /// <returns></returns>
   

    #endregion
    
}

