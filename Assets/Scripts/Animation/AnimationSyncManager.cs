﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AnimationSyncManager : MonoBehaviour
{
    #region Singelton
    public static AnimationSyncManager instance;

    private void Awake()
    {
        if (AnimationSyncManager.instance == null)
        {
            AnimationSyncManager.instance = this;
        }
    }
    #endregion

    public event Action onReadyToSyncTrigger;

    public void PlaySyncTrigger()
    {
        if (onReadyToSyncTrigger != null)
        {
            onReadyToSyncTrigger();
        }
    }
}