using System;
using System.Collections.Generic;
using System.Text;

namespace XaocSirck_Core.Interface.Feature;

internal interface IFeatureExtraction : IDisposable
{
    public void Extract();
    public IntPtr GetResult();
    public void Set(Object inputData);
    public void Clear();
}
