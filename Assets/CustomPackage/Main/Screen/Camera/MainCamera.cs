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
    public void ActiveSpriteBG(bool active) => Object?.ActiveSpriteBG(active);
    public void SetSpriteBG(Sprite sprite) => Object?.SetSpriteBG(sprite);
    public void SetSpriteColor(Color color) => Object?.SetSpriteColor(color);
}
