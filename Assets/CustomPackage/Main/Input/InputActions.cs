using UnityEngine;
 
 public abstract class InputActions
 {
     protected InputActions(InputManager manager)
     {
         Manager = manager;
     }
     protected InputManager Manager;
     protected Camera MainCamera => Camera.main;
     public abstract void Connect();
     public abstract void Disconnect();
 }