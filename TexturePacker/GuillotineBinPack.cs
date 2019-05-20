using System;
using System.Collections.Generic;

namespace TexturePacker
{
    [System.Runtime.InteropServices.Guid("12D8DD1A-97A5-4D13-8D50-76E51B9EA766")]
    public class GuillotineBinPack
    {
        /// The initial bin size will be (0,0). Call Init to set the bin size.
        public GuillotineBinPack()
        {
            Init(0, 0);
        }

        /// Initializes a new bin of the given size.
        public GuillotineBinPack(int width, int height)
        {
            Init(width, height);
        }

        /// (Re)initializes the packer to an empty bin of width x height units. Call whenever
        /// you need to restart with a new bin.
        public void Init(int width, int height)
        {
            binWidth = width;
            binHeight = height;

            usedRectangles = new List<BinRect>();
            freeRectangles = new List<BinRect>();

            // We start with a single big free rectangle that spans the whole bin.
            BinRect n;
            n.x = 0;
            n.y = 0;
            n.width = width;
            n.height = height;

            freeRectangles.Clear();
            freeRectangles.Add(n);
        }

        /// Specifies the different choice heuristics that can be used when deciding which of the free subrectangles
        /// to place the to-be-packed rectangle into.
        public enum FreeRectChoiceHeuristic
        {
            RectBestAreaFit, ///< -BAF
            RectBestShortSideFit, ///< -BSSF
            RectBestLongSideFit, ///< -BLSF
            RectWorstAreaFit, ///< -WAF
            RectWorstShortSideFit, ///< -WSSF
            RectWorstLongSideFit ///< -WLSF
        };

        /// Specifies the different choice heuristics that can be used when the packer needs to decide whether to
        /// subdivide the remaining free space in horizontal or vertical direction.
        public enum GuillotineSplitHeuristic
        {
            SplitShorterLeftoverAxis, ///< -SLAS
            SplitLongerLeftoverAxis, ///< -LLAS
            SplitMinimizeArea, ///< -MINAS, Try to make a single big rectangle at the expense of making the other small.
            SplitMaximizeArea, ///< -MAXAS, Try to make both remaining rectangles as even-sized as possible.
            SplitShorterAxis, ///< -SAS
            SplitLongerAxis ///< -LAS
        };

        /// Inserts a single rectangle into the bin. The packer might rotate the rectangle, in which case the returned
        /// struct will have the width and height values swapped.
        /// @param merge If true, performs free Rectangle Merge procedure after packing the new rectangle. This procedure
        ///		tries to defragment the list of disjoint free rectangles to improve packing performance, but also takes up 
        ///		some extra time.
        /// @param rectChoice The free rectangle choice heuristic rule to use.
        /// @param splitMethod The free rectangle split heuristic rule to use.
        public BinRect Insert(int width, int height, bool merge, FreeRectChoiceHeuristic rectChoice, GuillotineSplitHeuristic splitMethod)
        {
            // Find where to put the new rectangle.
            int freeNodeIndex = 0;
            BinRect newRect = FindPositionForNewNode(width, height, rectChoice, ref freeNodeIndex);

            // Abort if we didn't have enough space in the bin.
            if (newRect.height == 0)
                return newRect;

            // Remove the space that was just consumed by the new rectangle.
            {
                var item = freeRectangles[freeNodeIndex];
                SplitFreeRectByHeuristic(ref item, ref newRect, splitMethod);
                freeRectangles[freeNodeIndex] = item;
                freeRectangles.RemoveAt(freeNodeIndex);
            }
            // Perform a Rectangle Merge step if desired.
            if (merge)
                MergeFreeList();

            // Remember the new used rectangle.
            usedRectangles.Add(newRect);


            return newRect;

        }

        /// Inserts a list of rectangles into the bin.
        /// @param rects The list of rectangles to add. This list will be destroyed in the packing process.
        /// @param merge If true, performs Rectangle Merge operations during the packing process.
        /// @param rectChoice The free rectangle choice heuristic rule to use.
        /// @param splitMethod The free rectangle split heuristic rule to use.
        public void Insert(List<RectSize> rects, bool merge,
            FreeRectChoiceHeuristic rectChoice, GuillotineSplitHeuristic splitMethod)
        { // Remember variables about the best packing choice we have made so far during the iteration process.
            int bestFreeRect = 0;
            int bestRect = 0;
            bool bestFlipped = false;

            // Pack rectangles one at a time until we have cleared the rects array of all rectangles.
            // rects will get destroyed in the process.
            while (rects.Count > 0)
            {
                // Stores the penalty score of the best rectangle placement - bigger=worse, smaller=better.
                int bestScore = int.MaxValue;

                for (int i = 0; i < freeRectangles.Count; ++i)
                {
                    for (int j = 0; j < rects.Count; ++j)
                    {
                        // If this rectangle is a perfect match, we pick it instantly.
                        if (rects[j].width == freeRectangles[i].width && rects[j].height == freeRectangles[i].height)
                        {
                            bestFreeRect = i;
                            bestRect = j;
                            bestFlipped = false;
                            bestScore = int.MinValue;
                            i = freeRectangles.Count; // Force a jump out of the outer loop as well - we got an instant fit.
                            break;
                        }
                        // If flipping this rectangle is a perfect match, pick that then.
                        else if (rects[j].height == freeRectangles[i].width && rects[j].width == freeRectangles[i].height)
                        {
                            bestFreeRect = i;
                            bestRect = j;
                            bestFlipped = true;
                            bestScore = int.MinValue;
                            i = freeRectangles.Count; // Force a jump out of the outer loop as well - we got an instant fit.
                            break;
                        }
                        // Try if we can fit the rectangle upright.
                        else if (rects[j].width <= freeRectangles[i].width && rects[j].height <= freeRectangles[i].height)
                        {
                            var item = freeRectangles[i];
                            int score = ScoreByHeuristic(rects[j].width, rects[j].height,ref item, rectChoice);
                            freeRectangles[i] = item;
                            if (score < bestScore)
                            {
                                bestFreeRect = i;
                                bestRect = j;
                                bestFlipped = false;
                                bestScore = score;
                            }
                        }
                        // If not, then perhaps flipping sideways will make it fit?
                        else if (rects[j].height <= freeRectangles[i].width && rects[j].width <= freeRectangles[i].height)
                        {
                            var item = freeRectangles[i];
                            int score = ScoreByHeuristic(rects[j].height, rects[j].width,ref item, rectChoice);
                            freeRectangles[i] = item;
                            if (score < bestScore)
                            {
                                bestFreeRect = i;
                                bestRect = j;
                                bestFlipped = true;
                                bestScore = score;
                            }
                        }
                    }
                }

                // If we didn't manage to find any rectangle to pack, abort.
                if (bestScore == int.MaxValue)
                    return;

                // Otherwise, we're good to go and do the actual packing.
                BinRect newNode;
                newNode.x = freeRectangles[bestFreeRect].x;
                newNode.y = freeRectangles[bestFreeRect].y;
                newNode.width = rects[bestRect].width;
                newNode.height = rects[bestRect].height;

                if (bestFlipped) {
                    int temp = newNode.width;
                    newNode.width = newNode.height;
                    newNode.height = temp;
                }

                // Remove the free space we lost in the bin.
                {
                    var item = freeRectangles[bestFreeRect];
                    SplitFreeRectByHeuristic(ref item, ref newNode, splitMethod);
                    freeRectangles[bestFreeRect] = item;
                    freeRectangles.RemoveAt(bestFreeRect);
                }
                // Remove the rectangle we just packed from the input list.
                rects.RemoveAt(bestRect);

                // Perform a Rectangle Merge step if desired.
                if (merge)
                    MergeFreeList();

                // Remember the new used rectangle.
                usedRectangles.Add(newNode);
            }

        }

      
        /// Computes the ratio of used/total surface area. 0.00 means no space is yet used, 1.00 means the whole bin is used.
        public float Occupancy()
        {
            ///\todo The occupancy rate could be cached/tracked incrementally instead
            ///      of looping through the list of packed rectangles here.
            ulong usedSurfaceArea = 0;
            for (int i = 0; i < usedRectangles.Count; ++i)
            {
                usedSurfaceArea += (ulong)(usedRectangles[i].width * usedRectangles[i].height);
            }
            return (float)usedSurfaceArea / (binWidth * binHeight);
        }

        /// Returns the internal list of disjoint rectangles that track the free area of the bin. You may alter this vector
        /// any way desired, as long as the end result still is a list of disjoint rectangles.
        public List<BinRect> GetFreeRectangles() { return freeRectangles; }

        /// Returns the list of packed rectangles. You may alter this vector at will, for example, you can move a Rect from
        /// this list to the Free Rectangles list to free up space on-the-fly, but notice that this causes fragmentation.
        public List<BinRect> GetUsedRectangles() { return usedRectangles; }

        /// Performs a Rectangle Merge operation. This procedure looks for adjacent free rectangles and merges them if they
        /// can be represented with a single rectangle. Takes up Theta(|freeRectangles|^2) time.
        public void MergeFreeList()
        {
            // Do a Theta(n^2) loop to see if any pair of free rectangles could me merged into one.
            // Note that we miss any opportunities to merge three rectangles into one. (should call this function again to detect that)
            for (int i = 0; i < freeRectangles.Count; ++i)
                for (int j = i + 1; j < freeRectangles.Count; ++j)
                {
                    if (freeRectangles[i].width == freeRectangles[j].width && freeRectangles[i].x == freeRectangles[j].x)
                    {
                        if (freeRectangles[i].y == freeRectangles[j].y + freeRectangles[j].height)
                        {
                            var item = freeRectangles[i];
                            item.y -= freeRectangles[j].height;
                            item.height += freeRectangles[j].height;
                            freeRectangles[i] = item;
                            freeRectangles.RemoveAt(j);
                            --j;
                        }
                        else if (freeRectangles[i].y + freeRectangles[i].height == freeRectangles[j].y)
                        {
                            var item = freeRectangles[i];
                            item.height += freeRectangles[j].height;
                            freeRectangles[i] = item;
                            freeRectangles.RemoveAt( j);
                            --j;
                        }
                    }
                    else if (freeRectangles[i].height == freeRectangles[j].height && freeRectangles[i].y == freeRectangles[j].y)
                    {
                        if (freeRectangles[i].x == freeRectangles[j].x + freeRectangles[j].width)
                        {
                            var item = freeRectangles[i];
                            item.x -= freeRectangles[j].width;
                            item.width += freeRectangles[j].width;
                            freeRectangles[i] = item;
                            freeRectangles.RemoveAt( j);
                            --j;
                        }
                        else if (freeRectangles[i].x + freeRectangles[i].width == freeRectangles[j].x)
                        {
                            var item = freeRectangles[i];
                            item.width += freeRectangles[j].width;
                            freeRectangles[i] = item;
                            freeRectangles.RemoveAt(j);
                            --j;
                        }
                    }
                }
        }

        int binWidth;
        int binHeight;

        /// Stores a list of all the rectangles that we have packed so far. This is used only to compute the Occupancy ratio,
        /// so if you want to have the packer consume less memory, this can be removed.
        private List<BinRect> usedRectangles;

        /// Stores a list of rectangles that represents the free area of the bin. This rectangles in this list are disjoint.
        private List<BinRect> freeRectangles;


        /// Goes through the list of free rectangles and finds the best one to place a rectangle of given size into.
        /// Running time is Theta(|freeRectangles|).
        /// @param nodeIndex [out] The index of the free rectangle in the freeRectangles array into which the new
        ///		rect was placed.
        /// @return A Rect structure that represents the placement of the new rect into the best free rectangle.
        public BinRect FindPositionForNewNode(int width, int height, FreeRectChoiceHeuristic rectChoice,ref int nodeIndex)
        {
            BinRect bestNode = new BinRect(); 

            int bestScore = int.MaxValue;

            /// Try each free rectangle to find the best one for placement.
            for (int i = 0; i < freeRectangles.Count; ++i)
            {
                // If this is a perfect fit upright, choose it immediately.
                if (width == freeRectangles[i].width && height == freeRectangles[i].height)
                {
                    bestNode.x = freeRectangles[i].x;
                    bestNode.y = freeRectangles[i].y;
                    bestNode.width = width;
                    bestNode.height = height;
                    bestScore = int.MinValue;
                    break;
                }
                // If this is a perfect fit sideways, choose it.
                else if (height == freeRectangles[i].width && width == freeRectangles[i].height)
                {
                    bestNode.x = freeRectangles[i].x;
                    bestNode.y = freeRectangles[i].y;
                    bestNode.width = height;
                    bestNode.height = width;
                    bestScore = int.MinValue;
                    break;
                }
                // Does the rectangle fit upright?
                else if (width <= freeRectangles[i].width && height <= freeRectangles[i].height)
                {
                    var item = freeRectangles[i];
                    int score = ScoreByHeuristic(width, height,ref item, rectChoice);
                    freeRectangles[i] = item;
                    if (score < bestScore)
                    {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = width;
                        bestNode.height = height;
                        bestScore = score;
                    }
                }
                // Does the rectangle fit sideways?
                else if (height <= freeRectangles[i].width && width <= freeRectangles[i].height)
                {
                    var item = freeRectangles[i];
                    int score = ScoreByHeuristic(width, height, ref item, rectChoice);
                    freeRectangles[i] = item;
                    if (score < bestScore)
                    {
                        bestNode.x = freeRectangles[i].x;
                        bestNode.y = freeRectangles[i].y;
                        bestNode.width = height;
                        bestNode.height = width;
                        bestScore = score;
                    }
                }
            }
            return bestNode;
        }

        static int ScoreByHeuristic(int width, int height, ref BinRect freeRect, FreeRectChoiceHeuristic rectChoice)
        {
            switch (rectChoice)
            {
                case FreeRectChoiceHeuristic.RectBestAreaFit: return ScoreBestAreaFit(width, height,ref freeRect);
                case FreeRectChoiceHeuristic.RectBestShortSideFit: return ScoreBestShortSideFit(width, height,ref freeRect);
                case FreeRectChoiceHeuristic.RectBestLongSideFit: return ScoreBestLongSideFit(width, height,ref freeRect);
                case FreeRectChoiceHeuristic.RectWorstAreaFit: return ScoreWorstAreaFit(width, height,ref freeRect);
                case FreeRectChoiceHeuristic.RectWorstShortSideFit: return ScoreWorstShortSideFit(width, height,ref freeRect);
                case FreeRectChoiceHeuristic.RectWorstLongSideFit: return ScoreWorstLongSideFit(width, height,ref freeRect);
                default: return int.MaxValue;
            }
        }

        // The following functions compute (penalty) score values if a rect of the given size was placed into the 
        // given free rectangle. In these score values, smaller is better.

        static int ScoreBestAreaFit(int width, int height, ref BinRect freeRect)
        {
            return freeRect.width * freeRect.height - width * height;
        }
        static int ScoreBestShortSideFit(int width, int height, ref BinRect freeRect)
        {
            int leftoverHoriz = Math.Abs(freeRect.width - width);
            int leftoverVert = Math.Abs(freeRect.height - height);
            int leftover = Math.Min(leftoverHoriz, leftoverVert);
            return leftover;
        }
        static int ScoreBestLongSideFit(int width, int height, ref BinRect freeRect)
        {
            int leftoverHoriz = Math.Abs(freeRect.width - width);
            int leftoverVert = Math.Abs(freeRect.height - height);
            int leftover = Math.Max(leftoverHoriz, leftoverVert);
            return leftover;
        }

        static int ScoreWorstAreaFit(int width, int height, ref BinRect freeRect)
        {
            return -ScoreBestAreaFit(width, height,ref freeRect);
        }
        static int ScoreWorstShortSideFit(int width, int height, ref BinRect freeRect)
        {
            return -ScoreBestShortSideFit(width, height,ref freeRect);
        }
        static int ScoreWorstLongSideFit(int width, int height, ref BinRect freeRect)
        {
            return -ScoreBestLongSideFit(width, height,ref freeRect);
        }

        /// Splits the given L-shaped free rectangle into two new free rectangles after placedRect has been placed into it.
        /// Determines the split axis by using the given heuristic.
        public void SplitFreeRectByHeuristic(ref BinRect freeRect,  ref BinRect placedRect, GuillotineSplitHeuristic method)
        {
            // Compute the lengths of the leftover area.
            int w = freeRect.width - placedRect.width;
            int h = freeRect.height - placedRect.height;

            // Placing placedRect into freeRect results in an L-shaped free area, which must be split into
            // two disjoint rectangles. This can be achieved with by splitting the L-shape using a single line.
            // We have two choices: horizontal or vertical.	

            // Use the given heuristic to decide which choice to make.

            bool splitHorizontal;
            switch (method)
            {
                case GuillotineSplitHeuristic.SplitShorterLeftoverAxis:
                    // Split along the shorter leftover axis.
                    splitHorizontal = (w <= h);
                    break;
                case GuillotineSplitHeuristic.SplitLongerLeftoverAxis:
                    // Split along the longer leftover axis.
                    splitHorizontal = (w > h);
                    break;
                case GuillotineSplitHeuristic.SplitMinimizeArea:
                    // Maximize the larger area == minimize the smaller area.
                    // Tries to make the single bigger rectangle.
                    splitHorizontal = (placedRect.width * h > w * placedRect.height);
                    break;
                case GuillotineSplitHeuristic.SplitMaximizeArea:
                    // Maximize the smaller area == minimize the larger area.
                    // Tries to make the rectangles more even-sized.
                    splitHorizontal = (placedRect.width * h <= w * placedRect.height);
                    break;
                case GuillotineSplitHeuristic.SplitShorterAxis:
                    // Split along the shorter total axis.
                    splitHorizontal = (freeRect.width <= freeRect.height);
                    break;
                case GuillotineSplitHeuristic.SplitLongerAxis:
                    // Split along the longer total axis.
                    splitHorizontal = (freeRect.width > freeRect.height);
                    break;
                default:
                    splitHorizontal = true;
                    break;
            }

            // Perform the actual split.
            SplitFreeRectAlongAxis(ref freeRect,ref placedRect, splitHorizontal);
        }

        /// Splits the given L-shaped free rectangle into two new free rectangles along the given fixed split axis.
        public void SplitFreeRectAlongAxis(ref BinRect freeRect, ref BinRect placedRect, bool splitHorizontal)
        {
            // Form the two new rectangles.
            BinRect bottom = new BinRect();
            bottom.x = freeRect.x;
            bottom.y = freeRect.y + placedRect.height;
            bottom.height = freeRect.height - placedRect.height;

            BinRect right = new BinRect();
            right.x = freeRect.x + placedRect.width;
            right.y = freeRect.y;
            right.width = freeRect.width - placedRect.width;

            if (splitHorizontal)
            {
                bottom.width = freeRect.width;
                right.height = placedRect.height;
            }
            else // Split vertically
            {
                bottom.width = placedRect.width;
                right.height = freeRect.height;
            }

            // Add the new rectangles into the free rectangle pool if they weren't degenerate.
            if (bottom.width > 0 && bottom.height > 0)
                freeRectangles.Add(bottom);
            if (right.width > 0 && right.height > 0)
                freeRectangles.Add(right);
        }

        public bool IsContainedIn(BinRect a, BinRect b)
        {
            return a.x >= b.x && a.y >= b.y
            && a.x + a.width <= b.x + b.width
            && a.y + a.height <= b.y + b.height;
        }
    }
}