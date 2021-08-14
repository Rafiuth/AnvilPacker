using System;
using System.Collections.Generic;
using AnvilPacker.Level;
using AnvilPacker.Level.Physics;
using Xunit;

namespace AnvilPacker.Tests
{
    public class VoxelShapeTests
    {
        [Theory]
        [MemberData(nameof(GetTestShapes))]
        public void Test_MergedFacesOccludes(VoxelShape shapeA, VoxelShape shapeB, Direction trueDirs)
        {
            var actualDirs = Direction.None;
            foreach (var dir in Directions.All) {
                bool result = VoxelShape.MergedFacesOccludes(shapeA, shapeB, dir);
                actualDirs |= result ? dir : 0;
            }
            Assert.True(actualDirs == trueDirs, $"Incorrect result; actual={actualDirs} expected={trueDirs} shapes={shapeA};{shapeB}");
        }

        public static IEnumerable<object[]> GetTestShapes()
        {
            var cubeOutline = new[] {
                //bottom ring
                0, 0, 0, 16, 2, 2,
                0, 0, 2, 2, 2, 16,
                14, 0, 2, 16, 2, 16,
                2, 0, 14, 14, 2, 16, 
                //top ring
                0, 14, 0, 16, 16, 2,
                0, 14, 2, 2, 16, 16,
                14, 14, 2, 16, 16, 16,
                2, 14, 14, 14, 16, 16, 
                //columns
                0, 2, 0, 2, 14, 2,
                14, 2, 0, 16, 14, 2,
                0, 2, 14, 2, 14, 16,
                14, 2, 14, 16, 14, 16
            };

            yield return Make(
                //Normal cube
                new[] { 0, 0, 0, 16, 16, 16 },
                new[] { 0, 0, 0, 16, 16, 16 },
                Direction.All
            );
            yield return Make(
                //Y halfs
                new[] { 0, 0, 0, 16, 8, 16 },
                new[] { 0, 8, 0, 16, 16, 16 },
                Direction.All & ~Direction.YPos
            );
            yield return Make(
                //X halfs
                new[] { 0, 0, 0, 8, 16, 16 },
                new[] { 8, 0, 0, 16, 16, 16 },
                Direction.All & ~Direction.XPos
            );
            yield return Make(
                //Z halfs
                new[] { 0, 0, 0, 16, 16, 8 },
                new[] { 0, 0, 8, 16, 16, 16 },
                Direction.All & ~Direction.ZPos
            );

            yield return Make(
                //Ring + cylinder touching XPos
                cubeOutline,
                new[] { 0, 2, 2, 14, 14, 14 },
                Direction.XPos
            );
            yield return Make(
                //Ring + cylinder touching XNeg and XPos
                cubeOutline,
                new[] { 0, 2, 2, 16, 14, 14 },
                Direction.XNeg | Direction.XPos
            );
            yield return Make(
                //Ring + cylinder touching YNeg and YPos
                cubeOutline,
                new[] { 2, 0, 2, 14, 16, 14 },
                Direction.YNeg | Direction.YPos
            );

            yield return Make(
                //Ring + thing touching all horz holes except borders
                cubeOutline,
                new[] { 
                    0, 2, 2, 2, 14, 14,
                    14, 2, 2, 16, 14, 14,
                    2, 2, 0, 14, 14, 2,
                    2, 2, 14, 14, 14, 16
                },
                Direction.AllHorz
            );

            static object[] Make(int[] a, int[] b, Direction dirs)
            {
                return new object[] { CreateShape(a), CreateShape(b), dirs };
            }
            static VoxelShape CreateShape(int[] coords)
            {
                var boxes = new Box8[coords.Length / 6];
                for (int i = 0; i < boxes.Length; i++) {
                    boxes[i] = new Box8(
                        coords[i * 6 + 0],
                        coords[i * 6 + 1],
                        coords[i * 6 + 2],
                        coords[i * 6 + 3],
                        coords[i * 6 + 4],
                        coords[i * 6 + 5]
                    );
                }
                return new VoxelShape(boxes);
            }
        }

        /*
To inspect shapes, edit the bbs and pos arrays in draw().
https://editor.p5js.org/

function setup() {
  createCanvas(600, 400, WEBGL);
}

function draw() {
  background(16);

  ambientLight(200, 200, 200);

  push();
  //rotateZ(frameCount * 0.01);
  //rotateY(frameCount * 0.03);
  //rotateY(frameCount * 0.01);
  angleMode(DEGREES);
  rotateX(-25);
  rotateY(-15+frameCount*0.5);
  scale(1,-1,1);
  translate(-100,-50,0);
  
  let bbs = [
    [0, 0, 0, 16, 8, 16],
    [0, 8, 0, 16, 16, 16],
  ];
  let pos = [
    [0,0,0], [1,0,0]
  ];
  let i = 0;
  for (let bb of bbs) {
    push();
    scale(8);
    translate(pos[i][0]*16 + bb[0] + (bb[3]-bb[0])/2, 
              pos[i][1]*16 + bb[1] + (bb[4]-bb[1])/2, 
              pos[i][2]*16 + bb[2] + (bb[5]-bb[2])/2);
    fill(255, 255-i*128/bbs.length, 255-i*128/bbs.length);
    box(bb[3]-bb[0], bb[4]-bb[1], bb[5]-bb[2]);
    pop();
    i++;
  }
  
  ambientLight(255, 255, 255);
  noStroke();
  fill(255,0,0); box(1000,1,1);
  fill(0,255,0); box(1,1000,1);
  fill(0,0,255); box(1,1,1000);
  
  pop();
}
*/
    }
}