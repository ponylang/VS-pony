// Guids.cs
// MUST match guids.h
using System;

namespace Pony
{
    static class GuidList
    {
        public const string guidPonyLanguagePkgString = "56eb6247-5614-441a-b6b2-b5254f271edf";
        public const string guidPonyLanguageCmdSetString = "688293a9-24f4-4c4f-9143-e254b64eaff4";

        public static readonly Guid guidPonyLanguageCmdSet = new Guid(guidPonyLanguageCmdSetString);
    };
}