using UnityEngine;

public class KeepMusic : MonoBehaviour
{
    // Tạo một biến static để kiểm tra xem đã có nhạc nền nào tồn tại chưa
    private static KeepMusic instance;

    void Awake()
    {
        // Nếu đã có một object nhạc nền từ Scene trước mang sang rồi...
        if (instance != null)
        {
            // ...thì tự tiêu diệt bản sao mới mọc ra ở Scene này (để tránh 2 bài nhạc phát chồng lên nhau)
            Destroy(gameObject);
            return;
        }

        // Nếu chưa có, gán object này làm bản chính
        instance = this;
        
        // Yêu cầu Unity KHÔNG ĐƯỢC XÓA object này khi chuyển Scene
        DontDestroyOnLoad(gameObject);
    }
}