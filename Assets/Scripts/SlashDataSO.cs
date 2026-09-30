using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewSlashData", menuName = "Combat/Slash Data")]
public class SlashDataSO : ScriptableObject
{
    [Header("Mesh Settings")]
    public int segmentsPerSection = 12;
    public float tangentMultiplier = 0.5f;
    public bool doubleSided = true;
    public Material slashMaterial;

    [Header("Control Points Config")]
    public List<ControlPointData> controlPoints = new List<ControlPointData>()
    {
        new ControlPointData(new Vector3(-1.5f, 0, 0.5f), new Vector3(0, -30, 0), 1.5f),
        new ControlPointData(new Vector3(0f, 0.5f, 1.8f), new Vector3(0, 0, 0), 2.2f),
        new ControlPointData(new Vector3(1.5f, 0, 0.5f), new Vector3(0, 30, 0), 1.2f)
    };
}
