﻿// ***********************************************************************
// Copyright (c) 2017 Unity Technologies. All rights reserved.
//
// Licensed under the ##LICENSENAME##.
// See LICENSE.md file in the project root for full license information.
// ***********************************************************************

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FbxExporters.Review
{
    public class RotateModel : MonoBehaviour
    {

        [Tooltip ("Rotation speed in degrees/second")]
        [SerializeField]
        private float speed = 10f;

#if UNITY_EDITOR
        private float timeOfLastUpdate = float.MaxValue;
#endif

        private float deltaTime = 0;

        public float GetSpeed()
        {
            return speed;
        }

        public void Rotate()
        {
#if UNITY_EDITOR
            deltaTime = Time.realtimeSinceStartup - timeOfLastUpdate;
            if(deltaTime <= 0){
                deltaTime = 0.001f;
            }
            timeOfLastUpdate = Time.realtimeSinceStartup;
#else
            deltaTime = Time.deltaTime;
#endif
            transform.Rotate (Vector3.up, speed * deltaTime, Space.World); 
        }

        void Update ()
        {
            Rotate ();
        }
    }
}