using System;
using UnityEngine;

public class ParticleReference :Reference
{
   
    void OnParticleSystemStopped()
    {
        Release();
    }
}