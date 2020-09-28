using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Firebase;
using Firebase.Extensions;
using System.Threading.Tasks;

public class FirebaseInit : MonoBehaviour
{
    public UnityEvent OnFirebaseInitialized = new UnityEvent();
    public Task firebaseInitTask { get; private set; }
    public bool isFirebaseInitialized { get; private set; }

    // Start is called before the first frame update
    void Start()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            firebaseInitTask = task;

            if (task.Exception != null)
            {
                Debug.LogError(message: $"Failed to initialized Firebase with {task.Exception}.");
                return;
            }

            if (task.IsCompleted)
            {
                isFirebaseInitialized = true;
                OnFirebaseInitialized.Invoke();
            }
        });
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
