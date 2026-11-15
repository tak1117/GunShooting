//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using UnityEngine;
using System.Collections;
using System;
using Valve.VR;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Collections.Generic;

namespace Valve.VR
{
    public static class SteamVR_Input_Source
    {
        public static int numSources = System.Enum.GetValues(typeof(SteamVR_Input_Sources)).Length;

        private static ulong[] inputSourceHandlesBySource;
        private static Dictionary<ulong, SteamVR_Input_Sources> inputSourceSourcesByHandle = new Dictionary<ulong, SteamVR_Input_Sources>();

        private static Type enumType = typeof(SteamVR_Input_Sources);
        private static Type descriptionType = typeof(DescriptionAttribute);

        private static SteamVR_Input_Sources[] allSources;

        public static ulong GetHandle(SteamVR_Input_Sources inputSource)
        {
            int index = (int)inputSource;
            if (index < inputSourceHandlesBySource.Length)
                return inputSourceHandlesBySource[index];

            return 0;
        }
        public static SteamVR_Input_Sources GetSource(ulong handle)
        {
            if (inputSourceSourcesByHandle.ContainsKey(handle))
                return inputSourceSourcesByHandle[handle];

            return SteamVR_Input_Sources.Any;
        }

        public static SteamVR_Input_Sources[] GetAllSources()
        {
            if (allSources == null)
                allSources = (SteamVR_Input_Sources[])System.Enum.GetValues(typeof(SteamVR_Input_Sources));

            return allSources;
        }

        private static string GetPath(string inputSourceEnumName)
        {
            return ((DescriptionAttribute)enumType.GetMember(inputSourceEnumName)[0].GetCustomAttributes(descriptionType, false)[0]).Description;
        }

        // On some setups OpenVR's input system isn't done coming up yet when this first
        // runs from SteamVR_Behaviour.Awake(), so GetInputSourceHandle briefly returns
        // NoData for every path (including the hands), leaving their handles stuck at 0
        // and the controllers never showing up. Retry a few times before giving up.
        // How long OpenVR's input system takes to come up after SteamVR connects varies
        // run to run (observed anywhere from under a second to several seconds), so the
        // retry budget needs enough headroom to cover the slow cases too.
        private const int MaxInitializeAttempts = 25;
        private const int InitializeRetryDelayMs = 200;

        private static readonly SteamVR_Input_Sources[] criticalSources =
        {
            SteamVR_Input_Sources.LeftHand,
            SteamVR_Input_Sources.RightHand
        };

        public static void Initialize()
        {
            for (int attempt = 1; attempt <= MaxInitializeAttempts; attempt++)
            {
                bool isLastAttempt = attempt == MaxInitializeAttempts;
                bool criticalSourceFailed = ResolveAllHandles(logErrors: isLastAttempt);

                if (!criticalSourceFailed || isLastAttempt)
                    return;

                System.Threading.Thread.Sleep(InitializeRetryDelayMs);
            }
        }

        // Resolves every input source's handle in one pass and publishes the result.
        // Returns true if a hand (controller) failed to resolve, telling the caller
        // whether it's worth retrying.
        private static bool ResolveAllHandles(bool logErrors)
        {
            string[] enumNames = System.Enum.GetNames(enumType);
            ulong[] handlesBySource = new ulong[enumNames.Length];
            Dictionary<ulong, SteamVR_Input_Sources> sourcesByHandle = new Dictionary<ulong, SteamVR_Input_Sources>();
            List<SteamVR_Input_Sources> sourcesList = new List<SteamVR_Input_Sources>(enumNames.Length);
            bool criticalSourceFailed = false;

            for (int enumIndex = 0; enumIndex < enumNames.Length; enumIndex++)
            {
                SteamVR_Input_Sources source = (SteamVR_Input_Sources)enumIndex;
                string path = GetPath(enumNames[enumIndex]);

                ulong handle = 0;
                EVRInputError err = OpenVR.Input.GetInputSourceHandle(path, ref handle);

                if (err != EVRInputError.None)
                {
                    if (Array.IndexOf(criticalSources, source) >= 0)
                        criticalSourceFailed = true;

                    if (logErrors)
                        Debug.LogError("<b>[SteamVR]</b> GetInputSourceHandle (" + path + ") error: " + err.ToString());
                }

                if (source == SteamVR_Input_Sources.Any) //todo: temporary hack
                    handle = 0;

                handlesBySource[enumIndex] = handle;
                sourcesByHandle[handle] = source;
                sourcesList.Add(source);
            }

            inputSourceHandlesBySource = handlesBySource;
            inputSourceSourcesByHandle = sourcesByHandle;
            allSources = sourcesList.ToArray();

            return criticalSourceFailed;
        }
    }
}