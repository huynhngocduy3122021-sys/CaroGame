using UnityEngine;

public class AudioManager : MonoBehaviour
{
    [Header("Gắn Audio Source vào đây")]
    public AudioSource backgroundMusic;

    [Header("Gắn 2 nút vào đây")]
    public GameObject soundOnButton;
    public GameObject soundOffButton;

    // Hàm gọi khi bấm nút Tắt nhạc
    public void TurnOffSound()
    {
        backgroundMusic.mute = true; // Tắt tiếng
        soundOnButton.SetActive(false); // Ẩn nút Sound On
        soundOffButton.SetActive(true); // Hiện nút Sound Off
    }

    // Hàm gọi khi bấm nút Bật nhạc
    public void TurnOnSound()
    {
        backgroundMusic.mute = false; // Bật lại tiếng
        soundOffButton.SetActive(false); // Ẩn nút Sound Off
        soundOnButton.SetActive(true); // Hiện nút Sound On
    }
}