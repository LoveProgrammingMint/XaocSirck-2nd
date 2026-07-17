using System;
using System.Collections.Generic;
using System.Text;

namespace XaocSirck_Core.Interface.Engine;

public enum _Mode_Bitremal : Byte
{
    Disabled,
    Rb,
    Al,
    Ot,
    Ot_Exist_RB
}

public enum _Mode_Zeroflows : Byte
{
    Disabled,
    Zf
}

public enum _Mode_Signature : Byte
{
    Disabled,
    Loose,
    Strict
}

public enum _Mode_Documentation : Byte
{
    Disabled,
    DocVBA
}

public enum _Mode_Shell : Byte
{
    Disabled,
    Block,
    Suspicious
}

public enum _Mode_Archive : Byte
{
    Disabled,
    Check
}

public struct _Mode_Engines
{
    public _Mode_Signature Signature;
    public _Mode_Archive Archive;
    public _Mode_Documentation Documentation;
    public _Mode_Shell Shell;
}

public enum _Mode_Charwolf : Byte
{
    Disabled,
    Core,
    Extended,
    Fulled
}

public struct EngineMode
{
    public _Mode_Bitremal Bitremal;
    public _Mode_Zeroflows Zeroflow;
    public _Mode_Signature Signature;
    public _Mode_Charwolf Charwolf;
    public _Mode_Engines Engines;
}
