using UnityEngine;

[CreateAssetMenu(fileName = "SO_AudioOption", menuName = "ScriptableObjects/Option/Audio")]
public class SO_AudioOption : ScriptableObject
{
    public float MasterVolume;
    public float BGMVolume;
    public float SFXVolume;
}