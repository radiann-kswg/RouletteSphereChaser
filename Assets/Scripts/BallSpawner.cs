using System.Collections;
using UnityEngine;

/// 起動時に番号付きボールを順次投入する。
public class BallSpawner : MonoBehaviour
{
    public GameObject ballPrefab;
    public int count = 8;
    public float interval = 1.5f;

    IEnumerator Start()
    {
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(ballPrefab, transform.position, Random.rotation);
            go.name = $"Ball_{i + 1:00}";
            var ball = go.GetComponent<LotteryBall>();
            ball.number = i + 1;
            ball.tint = Color.HSVToRGB((i * 0.618f) % 1f, 0.7f, 0.95f); // 黄金比で色相分散
            ball.Apply();
            yield return new WaitForSeconds(interval);
        }
    }
}
