﻿// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace FbxExporters.Review
{
    [CustomEditor (typeof(RotateModel))]
    public class EditorRotate : UnityEditor.Editor
    {
        RotateModel model;

        public void OnEnable ()
        {
            model = (RotateModel)target;
            EditorApplication.update += Update;
        }

        public void OnDisable ()
        {
            EditorApplication.update -= Update;
        }

        void Update ()
        {
            // don't do anything in play mode
            if (model == null || EditorApplication.isPlaying) {
                return;
            }
            model.Rotate ();
        }
    }
}