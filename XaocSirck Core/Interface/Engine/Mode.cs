using System;
using System.Collections.Generic;
using System.Text;

namespace XaocSirck_Core.Interface.Engine;

internal enum _Mode_Bitremal : Byte
{
    Disabled,
    Rb,
    Al,
    Ot,
    Ot_Exist_RB
}

internal enum _Mode_Zeroflows : Byte
{
    Disabled,
    Zf
}

internal enum _Mode_Signature : Byte
{
    Disabled,
    Loose,
    Strict
}

internal enum _Mode_Documentation : Byte
{
    Disabled,
    DocVBA
}

internal enum _Mode_Shell : Byte
{
    Disabled,
    Block,
    Suspicious
}

internal enum _Mode_Archive : Byte
{
    Disabled,
    Check
}

internal struct _Mode_Engines
{
    public _Mode_Signature Signature;
    public _Mode_Archive Archive;
    public _Mode_Documentation Documentation;
    public _Mode_Shell Shell;
}

internal enum _Mode_Charwolf : Byte
{
    Disabled,
    Core,
    Extended,
    Fulled
}

internal struct EngineMode
{
    public _Mode_Bitremal Bitremal;
    public _Mode_Zeroflows Zeroflow;
    public _Mode_Signature Signature;
    public _Mode_Charwolf Charwolf;
    public _Mode_Engines Engines;
}
