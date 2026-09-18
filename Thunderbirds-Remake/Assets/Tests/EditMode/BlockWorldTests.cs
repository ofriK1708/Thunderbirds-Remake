using System;
using NUnit.Framework;
using Thunderbirds.Rules;

public class BlockWorldTests
{
    private BlockWorld world;
    [SetUp] public void Setup(){world=new BlockWorld(new CellBox(-16,-8,32,17));}
    private GridBody Add(int id,BodyKind kind,int x,int y,int w=2,int h=2,int capacity=4)
    {var b=new GridBody(id,kind,new CellBox(x,y,w,h),capacity);world.Add(b);return b;}
    [Test] public void FloatingWallNeverFallsAndStopsShip()
    {
        var wall=Add(10,BodyKind.Wall,0,0,4,1);var ship=Add(1,BodyKind.Ship,-2,0);
        for(int i=0;i<10;i++)world.StepGravity();
        Assert.That(wall.Box.Y,Is.EqualTo(0));Assert.That(world.TryMove(1,1,0),Is.False);Assert.That(ship.Box.X,Is.EqualTo(-2));
    }
    [TestCase(2,4)] [TestCase(4,8)] public void BothShipsPushLightBlock(int width,int capacity)
    {
        Add(1,BodyKind.Ship,-width,0,width,2,capacity);var block=Add(10,BodyKind.LightBlock,0,0);
        Assert.That(world.TryMove(1,1,0),Is.True);Assert.That(block.Box.X,Is.EqualTo(1));
    }
    [TestCase(2,4)] [TestCase(4,8)] public void BothShipsLiftAndCarry(int width,int capacity)
    {
        Add(1,BodyKind.Ship,0,-2,width,2,capacity);var block=Add(10,BodyKind.LightBlock,0,0);
        Assert.That(world.TryMove(1,0,1),Is.True);Assert.That(block.Box.Y,Is.EqualTo(1));
        Assert.That(world.TryMove(1,1,0),Is.True);Assert.That(block.Box.X,Is.EqualTo(1));
        Assert.That(world.TryMove(1,0,-1),Is.True);Assert.That(block.Box.Y,Is.EqualTo(0));
    }
    [Test] public void BlockFallsOnlyAfterLastSupportIsGone()
    {
        Add(11,BodyKind.Wall,0,-1,2,1);var block=Add(10,BodyKind.LightBlock,0,0);Add(1,BodyKind.Ship,-2,0);
        world.StepGravity();Assert.That(block.Box.Y,Is.Zero);
        world.TryMove(1,1,0);world.StepGravity();Assert.That(block.Box.Y,Is.Zero);
        world.TryMove(1,1,0);world.StepGravity();Assert.That(block.Box.Y,Is.EqualTo(-1));
    }
    [TestCase(2,4)] [TestCase(4,8)] public void ShipCanEnterBelowFallingBlockThenLiftIt(int width,int capacity)
    {
        Add(1,BodyKind.Ship,-width,-2,width,2,capacity);var block=Add(10,BodyKind.LightBlock,0,2);
        world.StepGravity();Assert.That(block.Box.Y,Is.EqualTo(1));
        Assert.That(world.TryMove(1,1,0),Is.True);
        world.StepGravity();world.StepGravity();Assert.That(block.Box.Y,Is.Zero);
        Assert.That(world.Carriers()[10],Is.EqualTo(1));
        Assert.That(world.TryMove(1,0,1),Is.True);Assert.That(block.Box.Y,Is.EqualTo(1));
    }
    [Test] public void InactiveShipAlsoSupportsFallingBlock()
    {
        Add(1,BodyKind.Ship,-8,-2);Add(2,BodyKind.Ship,0,-2,4,2,8);var block=Add(10,BodyKind.LightBlock,1,4);
        for(int i=0;i<20;i++)world.StepGravity();
        Assert.That(block.Box.Y,Is.Zero);Assert.That(world.Carriers()[10],Is.EqualTo(2));
    }
    [Test] public void CeilingRefusesLiftWithoutMovingEitherBody()
    {
        var ship=Add(1,BodyKind.Ship,0,-2);var block=Add(10,BodyKind.LightBlock,0,0);Add(11,BodyKind.Wall,0,2,2,1);
        Assert.That(world.TryMove(1,0,1),Is.False);Assert.That(ship.Box.Y,Is.EqualTo(-2));Assert.That(block.Box.Y,Is.Zero);
    }
    [Test] public void SideWallReleasesCargoWhileShipSlidesOut()
    {
        Add(1,BodyKind.Ship,0,-2);var block=Add(10,BodyKind.LightBlock,0,0);Add(11,BodyKind.Wall,2,0,1,2);
        Assert.That(world.TryMove(1,1,0),Is.True);Assert.That(block.Box.X,Is.Zero);
        Assert.That(world.TryMove(1,1,0),Is.True);world.StepGravity();Assert.That(block.Box.Y,Is.EqualTo(-1));
    }
    [Test] public void LedgeSetsDownCargoWhenShipDescends()
    {
        Add(1,BodyKind.Ship,1,-2);var block=Add(10,BodyKind.LightBlock,0,0);Add(11,BodyKind.Wall,-1,-2,2,1);
        Assert.That(world.Carriers()[10],Is.EqualTo(1));
        Assert.That(world.TryMove(1,0,-1),Is.True);Assert.That(block.Box.Y,Is.EqualTo(-1));
        Assert.That(world.TryMove(1,0,-1),Is.True);Assert.That(block.Box.Y,Is.EqualTo(-1));Assert.That(world.Carriers().ContainsKey(10),Is.False);
    }
    [Test] public void BlockSharedWithLedgeDoesNotFollowShipSideways()
    {
        Add(1,BodyKind.Ship,1,-2);var block=Add(10,BodyKind.LightBlock,0,0);Add(11,BodyKind.Wall,-1,-1,2,1);
        Assert.That(world.TryMove(1,1,0),Is.True);Assert.That(block.Box.X,Is.Zero);
    }
    [Test] public void FullChainWeightIsCheckedAtomically()
    {
        var ship=Add(1,BodyKind.Ship,-2,0);var a=Add(10,BodyKind.LightBlock,0,0);var b=Add(11,BodyKind.LightBlock,2,0);
        Assert.That(world.TryMove(1,1,0),Is.False);Assert.That(ship.Box.X,Is.EqualTo(-2));Assert.That(a.Box.X,Is.Zero);Assert.That(b.Box.X,Is.EqualTo(2));
    }
    [Test] public void AtlasPushesTwoLightBlocks()
    {
        Add(1,BodyKind.Ship,-4,0,4,2,8);Add(10,BodyKind.LightBlock,0,0);var b=Add(11,BodyKind.LightBlock,2,0);
        Assert.That(world.TryMove(1,1,0),Is.True);Assert.That(b.Box.X,Is.EqualTo(3));
    }
    [Test] public void UnsupportedStackFallsTogetherAndStopsAtFloor()
    {
        var bottom=Add(10,BodyKind.LightBlock,0,0);var top=Add(11,BodyKind.LightBlock,0,2);
        world.StepGravity();Assert.That(bottom.Box.Y,Is.EqualTo(-1));Assert.That(top.Box.Y,Is.EqualTo(1));
        for(int i=0;i<20;i++)world.StepGravity();Assert.That(bottom.Box.Y,Is.EqualTo(-8));Assert.That(top.Box.Y,Is.EqualTo(-6));
    }
    [Test] public void InvalidOverlappingLevelIsRejected()
    {
        Add(1,BodyKind.Ship,0,0);
        Assert.Throws<ArgumentException>(()=>Add(10,BodyKind.Wall,1,1));
    }
    [Test] public void FallingBlockWinsCellBeforeShipMoves()
    {
        Add(1,BodyKind.Ship,0,0);var block=Add(10,BodyKind.LightBlock,0,3);
        world.StepGravity();Assert.That(block.Box.Y,Is.EqualTo(2));
        Add(11,BodyKind.Wall,0,4,2,1);
        Assert.That(world.TryMove(1,0,1),Is.False);
    }
    [Test] public void AuthoredDemoAllowsPushCatchAndLift()
    {
        Add(1,BodyKind.Ship,-12,-8);Add(2,BodyKind.Ship,-7,-8,4,2,8);
        Add(11,BodyKind.Wall,-12,-4,6,1);Add(12,BodyKind.Wall,2,1,7,1);
        var block=Add(10,BodyKind.LightBlock,-10,-3);
        Assert.That(world.TryMove(2,1,0),Is.True);
        for(int i=0;i<2;i++)Assert.That(world.TryMove(2,0,1),Is.True);
        for(int i=0;i<2;i++)Assert.That(world.TryMove(1,-1,0),Is.True);
        for(int i=0;i<5;i++)Assert.That(world.TryMove(1,0,1),Is.True);
        for(int i=0;i<6;i++){Assert.That(world.TryMove(1,1,0),Is.True);world.StepGravity();}
        Assert.That(block.Box.X,Is.EqualTo(-6));Assert.That(block.Box.Y,Is.EqualTo(-4));
        Assert.That(world.Carriers()[10],Is.EqualTo(2));
        Assert.That(world.TryMove(2,0,1),Is.True);Assert.That(block.Box.Y,Is.EqualTo(-3));
    }
}
