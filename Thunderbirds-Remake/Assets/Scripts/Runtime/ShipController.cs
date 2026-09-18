using System;
using System.Collections.Generic;
using Thunderbirds.Rules;
using UnityEngine;

public class ShipController : MonoBehaviour
{
    [SerializeField] private InputReader input;
    [SerializeField] private Transform kestrel;
    [SerializeField] private Transform atlas;
    [SerializeField, Min(.02f)] private float fallStepSeconds = .1f;
    private readonly Dictionary<int, Transform> views = new Dictionary<int, Transform>();
    private BlockWorld world;
    private bool kestrelActive = true;
    private float nextMoveTime, fallElapsed;

    private void Start()
    {
        try
        {
            if (input == null) throw new InvalidOperationException("InputReader is missing.");
            world = new BlockWorld(new CellBox(-16, -8, 32, 17));
            Register(1, BodyKind.Ship, kestrel, new Vector2Int(2, 2), 4);
            Register(2, BodyKind.Ship, atlas, new Vector2Int(4, 2), 8);
            int id = 10;
            foreach (var block in FindObjectsByType<LevelBlock>(FindObjectsSortMode.InstanceID))
            {
                if (block.Kind == BodyKind.Ship)
                    throw new InvalidOperationException("LevelBlock must be Wall or LightBlock.");
                if (block.Kind == BodyKind.LightBlock && block.Size.x * block.Size.y > 4)
                    throw new InvalidOperationException(block.name + " is a light block: maximum weight is four cells.");
                Register(id++, block.Kind, block.transform, block.Size, 0);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError("Invalid level setup: " + exception.Message, this);
            enabled = false;
        }
    }

    private void Register(int id, BodyKind kind, Transform view, Vector2Int size, int capacity)
    {
        if (view == null) throw new InvalidOperationException("A ship reference is missing.");
        float x = view.position.x - size.x * .5f, y = view.position.y - size.y * .5f;
        if (Mathf.Abs(x - Mathf.Round(x)) > .001f || Mathf.Abs(y - Mathf.Round(y)) > .001f)
            throw new InvalidOperationException(view.name + " must align with grid cells. Odd sizes need a half-unit center.");
        world.Add(new GridBody(id, kind, new CellBox(Mathf.RoundToInt(x), Mathf.RoundToInt(y), size.x, size.y), capacity));
        views.Add(id, view);
    }

    private void Update()
    {
        // Falls are resolved before input on the same frame.
        fallElapsed += Mathf.Min(Time.deltaTime, .25f);
        float step = Mathf.Max(.02f, fallStepSeconds);
        while (fallElapsed >= step) { world.StepGravity(); fallElapsed -= step; }
        if (input.SwitchPressed) { kestrelActive = !kestrelActive; nextMoveTime = Time.time; }
        Vector2 movement = input.MoveInput;
        if (movement.sqrMagnitude >= .01f && Time.time >= nextMoveTime)
        {
            int dx = 0, dy = 0;
            if (Mathf.Abs(movement.x) >= Mathf.Abs(movement.y)) dx = movement.x > 0 ? 1 : -1;
            else dy = movement.y > 0 ? 1 : -1;
            world.TryMove(kestrelActive ? 1 : 2, dx, dy);
            nextMoveTime = Time.time + (kestrelActive ? .08f : .16f);
        }
        foreach (var body in world.Bodies)
        {
            var view = views[body.Id];
            view.position = new Vector3(body.Box.X + body.Box.Width * .5f, body.Box.Y + body.Box.Height * .5f, view.position.z);
        }
    }
}
