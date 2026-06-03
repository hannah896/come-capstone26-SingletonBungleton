using Cysharp.Threading.Tasks;
using UnityEngine;

public class MainCamera
{
    public MainCameraObject Object { get; private set; }
    public Camera Camera => Object?.Camera;

    public void SetObject(MainCameraObject obj)
    {
        Object = obj;
    }

    public async UniTask GenerateObjectAsync()
    {
        GameObject obj = await Main.Resource.LoadAssetAsync<GameObject>("MainCameraObject");
        Object = MonoBehaviour.Instantiate(obj).GetComponent<MainCameraObject>();
        Object.Set(this);
    }
    
    public void SetColorCameraBG(Color color) => Object?.SetColorCameraBG(color);
}
