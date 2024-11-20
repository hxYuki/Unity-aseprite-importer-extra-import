using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Aseprite;
using UnityEngine;

//using DG.Tweening;
namespace Assets.Extras.ShapeAnimation
{
    public class SpriteShapeProcessor : AssetPostprocessor
    {
        static ShapeType? GetShapeType(string str) =>
            str switch
            {
                "Rectangle" or "Rect" => ShapeType.Rectangle,
                "Point" => ShapeType.Point,
                _ => null,
            };

        void OnPreprocessAsset()
        {
            if (assetImporter is AsepriteImporter aseImporter)
            {
                aseImporter.OnPostAsepriteImport += OnPostAsepriteImport;
            }
        }

        static void OnPostAsepriteImport(AsepriteImporter.ImportEventArgs args)
        {
            var file = args.importer.asepriteFile;
            //var file = AsepriteReader.ReadFile(args.context.assetPath);
            var ppp = args.importer.spritePixelsPerUnit;
            var pixelSize = new Vector2(1 / ppp, 1 / ppp);
            var canvasSize = new Vector2(file.width, file.height);

            var layers = file
                .frameData.Where(fd => fd.chunks.Any(ck => ck.chunkType == ChunkTypes.Layer))
                .SelectMany(fd =>
                    fd.chunks.Where(ck => ck.chunkType == ChunkTypes.Layer)
                        .Select((ck, i) => (ck as LayerChunk, i))
                );

            var shapeLayers = layers
                .Where(ly => ly.Item1.name.StartsWith("#"))
                .Select(ly =>
                {
                    var layerNamePair = ly.Item1.name.Split(':');
                    if (layerNamePair.Length < 2)
                    {
                        Debug.LogError(
                            $"Aseprite Post Processer: Asset: \"{args.context.assetPath}\" \nShape layer \"{ly.Item1.name}\" didn't contains shape type information."
                        );
                    }

                    var shapeType = GetShapeType(layerNamePair[1]);
                    if (shapeType == null)
                    {
                        Debug.LogError(
                            $"Aseprite Post Processer: Asset: \"{args.context.assetPath}\" \nShape type \"{layerNamePair[1]}\" is not supported."
                        );
                    }

                    return (
                        layerName: layerNamePair[0][1..],
                        layerIndex: ly.i,
                        shape: shapeType ?? ShapeType.Rectangle
                    );
                });

            var frames = file
                .frameData.Where(fd => fd.chunks.Any(ck => ck.chunkType == ChunkTypes.Cell))
                .ToArray();

            foreach (var layer in shapeLayers)
            {
                ShapeAnimation shapeAnimation = ScriptableObject.CreateInstance<ShapeAnimation>();
                shapeAnimation.name = $"{layer.layerName}";
                //shapeAnimation.ClipName = tag.name;
                shapeAnimation.ShapeType = layer.shape;
                //shapeAnimation.Loop = tag.noOfRepeats == 0;

                switch (layer.shape)
                {
                    case ShapeType.Point:
                        shapeAnimation.Points = frames
                            .Select(
                                (fd, frameI) =>
                                {
                                    var layerCell =
                                        fd.chunks.FirstOrDefault(ck =>
                                            ck.chunkType == ChunkTypes.Cell
                                            && (ck as CellChunk).layerIndex == layer.layerIndex
                                        ) as CellChunk;

                                    if (layerCell != null)
                                    {
                                        layerCell = GetLinkedCell(frames, layer, layerCell);

                                        return ImageToPoint(layerCell, pixelSize, canvasSize);
                                    }
                                    else
                                    {
                                        Debug.LogWarning(
                                            $"Aseprite Post Processer: Layer {layer.layerName} contains empty cell at frame {frameI + 1}, default point is (0, 0)."
                                        );
                                        return Vector2.zero;
                                    }
                                }
                            )
                            .ToArray();
                        //shapeAnimation.Duration = tagFrames.Select(fd => (float)fd.frameDuration).ToArray();
                        break;

                    case ShapeType.Rectangle:
                        shapeAnimation.Rects = frames
                            .Select(
                                (fd, frameI) =>
                                {
                                    var layerCell =
                                        fd.chunks.FirstOrDefault(ck =>
                                            ck.chunkType == ChunkTypes.Cell
                                            && (ck as CellChunk).layerIndex == layer.layerIndex
                                        ) as CellChunk;

                                    if (layerCell != null)
                                    {
                                        layerCell = GetLinkedCell(frames, layer, layerCell);

                                        return ImageToRect(layerCell, pixelSize, canvasSize);
                                    }
                                    else
                                    {
                                        Debug.LogWarning(
                                            $"Aseprite Post Processer: Layer {layer.layerName} contains empty cell at frame {frameI + 1}, default rect is (0, 0), (0, 0)."
                                        );
                                        return new Rect(Vector2.zero, Vector2.zero);
                                    }
                                }
                            )
                            .ToArray();
                        //shapeAnimation.Duration = tagFrames.Select(fd => (float)fd.frameDuration).ToArray();
                        break;
                    default:
                        // Invalid path
                        break;
                }
                args.context.AddObjectToAsset(shapeAnimation.name, shapeAnimation);
            }
            Debug.Log("Aseprite Post Processer: Asset changed, reprocessed.");
        }

        private static CellChunk GetLinkedCell(
            FrameData[] frames,
            (string layerName, int layerIndex, ShapeType shape) layer,
            CellChunk layerCell
        )
        {
            if (layerCell.cellType == CellTypes.LinkedCell)
            {
                layerCell =
                    frames[layerCell.linkedToFrame]
                        .chunks.FirstOrDefault(ck =>
                            ck.chunkType == ChunkTypes.Cell
                            && (ck as CellChunk).layerIndex == layer.layerIndex
                        ) as CellChunk;
            }

            return layerCell;
        }

        static bool ColorContainsAny(Color32 color)
        {
            return color.r != 0 || color.g != 0 || color.b != 0 || color.a != 0;
        }

        static Rect ImageToRect(in CellChunk cell, Vector2 pixelSize, Vector2 canvasSize)
        {
            var cellTopLeft = new Vector2(cell.posX, cell.posY);
            var cellSize = new Vector2(cell.width, cell.height);

            var canvasCenter = canvasSize / 2;

            var pMove = cellTopLeft - canvasCenter;
            pMove.y = -pMove.y;

            float xmin = pMove.x,
                xmax = pMove.x + cellSize.x;
            float ymin = pMove.y - cellSize.y,
                ymax = pMove.y;

            return Rect.MinMaxRect(
                xmin * pixelSize.x,
                ymin * pixelSize.y,
                xmax * pixelSize.x,
                ymax * pixelSize.y
            );
        }

        static Vector2 ImageToPoint(in CellChunk cell, Vector2 pixelSize, Vector2 canvasSize)
        {
            // from canvas top-left to center
            var cellTopLeft = new Vector2(cell.posX, cell.posY);
            var cellSize = new Vector2(cell.width, cell.height);

            var cellCenter = cellTopLeft + cellSize / 2;
            var canvasCenter = canvasSize / 2;

            var pMove = cellCenter - canvasCenter;
            //pMove.y *= -1;
            pMove.x *= pixelSize.x;
            pMove.y *= -pixelSize.y;
            return pMove;
        }
    }
}

