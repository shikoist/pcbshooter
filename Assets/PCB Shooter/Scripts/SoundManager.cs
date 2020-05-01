using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum VGCSoundEffects
{
    PlayerConnected,
    PlayerDisconnected
}

public class SoundManager : MonoBehaviour {
    public AudioClip[] audioClips;
    public AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }
	// Use this for initialization
	void Start () {
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    public void Play(VGCSoundEffects effect)
    {
        audioSource.clip = audioClips[(int)effect];
        audioSource.Play();
    }
}
