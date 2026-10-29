using MelonLoader;
using UnityEngine;

namespace CustomNPCFramework
{
    [RegisterTypeInIl2Cpp]
    public class CustomNPC : MonoBehaviour
    {
        public CustomNPC(IntPtr ptr) : base(ptr) { }
    }
}
