using System;

namespace Stryker.Abstractions.Options;

[Flags]
public enum UnityTestMode
{
    None = 0,
    EditMode = 1,
    PlayMode = 2,
    All = EditMode | PlayMode,
}
