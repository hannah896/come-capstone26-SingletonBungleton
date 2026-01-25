using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Blossom.Preference;
using UnityEngine.InputSystem;

public class MainInitializer : MonoBehaviour
{
    void Awake()
    {
        Main _ = Main.Instance;
        DontDestroyOnLoad(gameObject);
    }
}