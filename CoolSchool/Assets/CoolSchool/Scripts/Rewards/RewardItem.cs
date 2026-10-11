using System;
using UnityEngine;

namespace CoolSchool.Rewards
{
    /// <summary>One thing that can be won from a present, with the model shown in the 3D reveal.</summary>
    [Serializable]
    public class RewardItem
    {
        public string id;
        public RewardKind kind;
        [Tooltip("The model shown spinning when the present opens.")]
        public GameObject prefab;
        [Tooltip("Extra rotation of the model in the reveal, to make it stand nicely.")]
        public Vector3 revealEuler;
        [Tooltip("Makes the model bigger or smaller in the reveal.")]
        public float revealScale = 1f;
    }
}
