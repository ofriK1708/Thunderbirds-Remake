using UnityEngine;

public class ShipController : MonoBehaviour
{
    [SerializeField] private InputReader input;
    [SerializeField] private Transform kestrel;
    [SerializeField] private Transform atlas;

    private bool kestrelActive = true;
    private float nextMoveTime;

    private void Update()
    {
        if (input.SwitchPressed)
        {
            kestrelActive = !kestrelActive;
            nextMoveTime = Time.time;

            Debug.Log("Active ship: " +
                (kestrelActive ? "Kestrel" : "Atlas"));
        }

        Vector2 movement = input.MoveInput;

        if (movement.sqrMagnitude < 0.01f ||
            Time.time < nextMoveTime)
        {
            return;
        }

        // בוחרים כיוון אחד כדי למנוע תנועה באלכסון.
        Vector2 direction =
            Mathf.Abs(movement.x) >= Mathf.Abs(movement.y)
            ? new Vector2(Mathf.Sign(movement.x), 0)
            : new Vector2(0, Mathf.Sign(movement.y));

        Transform active = kestrelActive ? kestrel : atlas;
        Transform other = kestrelActive ? atlas : kestrel;

        Vector2 activeSize = kestrelActive
            ? new Vector2(2, 2)
            : new Vector2(4, 2);

        Vector2 otherSize = kestrelActive
            ? new Vector2(4, 2)
            : new Vector2(2, 2);

        Vector2 target = (Vector2)active.position + direction;

        if (CanMove(target, activeSize, other, otherSize))
        {
            active.position = new Vector3(
                target.x, target.y, active.position.z);
        }

        nextMoveTime = Time.time +
            (kestrelActive ? 0.08f : 0.16f);
    }

    private bool CanMove(
        Vector2 target,
        Vector2 size,
        Transform other,
        Vector2 otherSize)
    {
        Vector2 half = size / 2f;

        // גבולות זמניים: הרצפה בגובה 8- ושאר מסגרת הלוח.
        if (target.x - half.x < -16 ||
            target.x + half.x > 16 ||
            target.y - half.y < -8 ||
            target.y + half.y > 9)
        {
            return false;
        }

        Vector2 distance = target - (Vector2)other.position;
        Vector2 requiredGap = (size + otherSize) / 2f;

        bool overlaps =
            Mathf.Abs(distance.x) < requiredGap.x &&
            Mathf.Abs(distance.y) < requiredGap.y;

        return !overlaps;
    }
}