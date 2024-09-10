using System.Runtime.InteropServices.JavaScript;

namespace GenShaderBinding.GameApp.GameFramework;

public interface IShaderLoader
{
    JSObject LoadShaderProgram(string vertexShaderName, string fragmentShaderName);
}
