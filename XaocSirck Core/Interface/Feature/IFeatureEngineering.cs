using System;
using System.Collections.Generic;
using System.Text;

namespace XaocSirck_Core.Interface.Feature;

internal interface IFeatureEngineering : IDisposable
{
    public void Engineer();
    public IntPtr GetResult();
    public void Set(Object? inputData);
    public void Clear();
}
