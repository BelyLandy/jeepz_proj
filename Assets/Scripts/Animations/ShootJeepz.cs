using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class ShootJeepz : MonoBehaviour
{
    [SerializeField] private Animator _animator;

    private int _shootLayer;
    private  int _shootStateHash;

    private KeyControl _keyControl;

    private void Awake()
    {
        _shootLayer = _animator.GetLayerIndex("Shoot");
        _shootStateHash = Animator.StringToHash("Temp_Shoot");
        
        _keyControl = Keyboard.current.zKey;

        _animator.SetFloat("ShootSpeed", 2f);
    }

    void Update()
    {
        if (_keyControl != null && _keyControl.wasPressedThisFrame)
        {
            _animator.Play(_shootStateHash, _shootLayer, 0f);
            _animator.Update(0f);

        }
    }
}
