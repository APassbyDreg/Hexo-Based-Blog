---
title: PBRT 第四章笔记 | Notes for PBRT Chapter 04 - Primitives and Intersection Acceleration
date: 2021-10-31 00:58:53
categories: 
- programming
- reading notes
tags:
- CG
- render
- PBRT
toc: true
---

[[toc]]

# 4.1 Primitive Interface and Geometric Primitives

抽象的 `Primitive` 基类提供了连接几何系统和着色系统的桥梁

```cpp
// Primitive Declarations
class Primitive {
  public:
    // Primitive Interface
    virtual ~Primitive();
    virtual Bounds3f WorldBound() const = 0;
    virtual bool Intersect(const Ray &r, SurfaceInteraction *) const = 0;
    virtual bool IntersectP(const Ray &r) const = 0;
    virtual const AreaLight *GetAreaLight() const = 0;
    virtual const Material *GetMaterial() const = 0;
    virtual void ComputeScatteringFunctions(SurfaceInteraction *isect,
                                            MemoryArena &arena,
                                            TransportMode mode,
                                            bool allowMultipleLobes) const = 0;
};
```

前三个函数均在上一节中有所提及，第四个 `GetAreaLight` 函数则会返回一个指向该几何体的发光分布（emission distribution）的指针。这是为了统一作为光源的几何体和其它的几何体类型；第五个 `GetMaterial` 函数则返回了该几何体对应的材质信息。

最后一个函数 `ComputeScatteringFunctions` 会初始化参数 `isect` 中用于表示局部光线散射信息的表示（如 BSDF、BSSRDF 等）

## 4.1.1 Geometric Primitives

`Geometric Primitives` 类表示了场景中的单个物体，每个 `Geometric Primitives` 类中都有且只有一个 `Shape` 类型的实例。

```cpp
class GeometricPrimitive : public Primitive {
  public:
    // GeometricPrimitive Public Methods
    virtual Bounds3f WorldBound() const;
    virtual bool Intersect(const Ray &r, SurfaceInteraction *isect) const;
    virtual bool IntersectP(const Ray &r) const;
    GeometricPrimitive(const std::shared_ptr<Shape> &shape,
                       const std::shared_ptr<Material> &material,
                       const std::shared_ptr<AreaLight> &areaLight,
                       const MediumInterface &mediumInterface);
    const AreaLight *GetAreaLight() const;
    const Material *GetMaterial() const;
    void ComputeScatteringFunctions(SurfaceInteraction *isect,
                                    MemoryArena &arena, TransportMode mode,
                                    bool allowMultipleLobes) const;

  private:
    // GeometricPrimitive Private Data
    std::shared_ptr<Shape> shape;
    std::shared_ptr<Material> material;
    std::shared_ptr<AreaLight> areaLight;
    MediumInterface mediumInterface;
};
```

除了指向 `Shape` 的指针外，它还保存了指向光照和材质信息的指针，最后的 `mediumInterface` 则被用于体积渲染中。

本类中的前三个函数均基本都是重定向至其 `shape` 指针的对应函数上。`Intersect` 函数则额外增加了初始化 `isect` 参数中的 `mediumInterface` 成员的步骤。

## TransformedPrimitive: Object Instancing and Animated Primitives

`TransformedPrimitive` 类保存了一个 `Primitive` 和一个 `AnimatedTransform` 。这个额外的信息让 object instancing 和为几何体设置动画变得可能。

```cpp
class TransformedPrimitive : public Primitive {
  public:
    // TransformedPrimitive Public Methods
    TransformedPrimitive(std::shared_ptr<Primitive> &primitive,
                         const AnimatedTransform &PrimitiveToWorld);
    bool Intersect(const Ray &r, SurfaceInteraction *in) const;
    bool IntersectP(const Ray &r) const;
    const AreaLight *GetAreaLight() const { return nullptr; }
    const Material *GetMaterial() const { return nullptr; }
    void ComputeScatteringFunctions(SurfaceInteraction *isect,
                                    MemoryArena &arena, TransportMode mode,
                                    bool allowMultipleLobes) const { /* 这个函数不应该被运行，运行时会报错 */ }
    Bounds3f WorldBound() const {
        return PrimitiveToWorld.MotionBounds(primitive->WorldBound());
    }

  private:
    // TransformedPrimitive Private Data
    std::shared_ptr<Primitive> primitive;
    const AnimatedTransform PrimitiveToWorld;
};
```

本类中的 `GetAreaLight, GetMaterial, ComputeScatteringFunctions` 不能直接调用（会报错），真正需要调用的是这个对象拥有的 primitive 的对应函数。

> 不是很明白这里为啥不直接转发 `primitive` 成员的对应函数

### Object Instancing

我将这个词理解为物体的复用。当场景中存在大量重复的物体时，这些物体本质上只有其 transform 不同而已。在本类的情况下，可以让大量不同的 `TransformedPrimitive` 指向相同的 `Primitive` ，而只有 transform 不一样。

### 刚体动画

通过在底层 shape 之上再叠加一层变换可以给更加方便的动画设置提供支持。虽然这样会增加一部分成本，但让底层的 shape 知晓更多的变换信息会产生更多的内存和帧间处理的消耗，这与本类的初衷背道而驰

# 4.2 Aggregates

`Aggregate` 类提供了将多个几何体聚合为单个几何体的方式。这种将不同物体聚合而视作一个的方法在构建加速结构中非常常用。这也是一个抽象类，其中的任何函数都不该被调用。

```cpp
class Aggregate : public Primitive {
  public:
    // Aggregate Public Methods
    const AreaLight *GetAreaLight() const;
    const Material *GetMaterial() const;
    void ComputeScatteringFunctions(SurfaceInteraction *isect,
                                    MemoryArena &arena, TransportMode mode,
                                    bool allowMultipleLobes) const;
};
```

> 不是很明白这里不用就不用为啥不直接留空啊，基类的这几个函数本来就没有函数体

# 4.3 Bounding Volume Hierarchies (BVH)

<center><img src="https://pbr-book.org/3ed-2018/Primitives_and_Intersection_Acceleration/Primitives%20and%20hierarchy.svg" style="max-height: 40vh; margin: 10px"/></center>

BVH 以一种树状的结构加速光线相交测试。当光线与树中任意节点的包围盒没有相交时，该节点的整个子树都会被丢弃。在这种结构中，每一个几何体均在树中只出现一次，这种特性不仅避免了重复将同一个物体和光线求交，还使得这种加速算法的空间消耗有了明确的上界。

与下一节要讨论的 kd-tree 相比，BVH 虽然效率上稍微低一些，但优势在于构建时间较短、且不易受到各类数值误差的影响。

```cpp
class BVHAccel : public Aggregate {
  public:
    // BVHAccel Public Types
    enum class SplitMethod { SAH, HLBVH, Middle, EqualCounts };

    // BVHAccel Public Methods
    BVHAccel(std::vector<std::shared_ptr<Primitive>> p,
             int maxPrimsInNode = 1,
             SplitMethod splitMethod = SplitMethod::SAH);
    Bounds3f WorldBound() const;
    ~BVHAccel();
    bool Intersect(const Ray &ray, SurfaceInteraction *isect) const;
    bool IntersectP(const Ray &ray) const;

  private:
    // BVHAccel Private Methods
    BVHBuildNode *recursiveBuild(
        MemoryArena &arena, std::vector<BVHPrimitiveInfo> &primitiveInfo,
        int start, int end, int *totalNodes,
        std::vector<std::shared_ptr<Primitive>> &orderedPrims);
    BVHBuildNode *HLBVHBuild(
        MemoryArena &arena, const std::vector<BVHPrimitiveInfo> &primitiveInfo,
        int *totalNodes,
        std::vector<std::shared_ptr<Primitive>> &orderedPrims) const;
    BVHBuildNode *emitLBVH(
        BVHBuildNode *&buildNodes,
        const std::vector<BVHPrimitiveInfo> &primitiveInfo,
        MortonPrimitive *mortonPrims, int nPrimitives, int *totalNodes,
        std::vector<std::shared_ptr<Primitive>> &orderedPrims,
        std::atomic<int> *orderedPrimsOffset, int bitIndex) const;
    BVHBuildNode *buildUpperSAH(MemoryArena &arena,
                                std::vector<BVHBuildNode *> &treeletRoots,
                                int start, int end, int *totalNodes) const;
    int flattenBVHTree(BVHBuildNode *node, int *offset);

    // BVHAccel Private Data
    const int maxPrimsInNode;
    const SplitMethod splitMethod;
    std::vector<std::shared_ptr<Primitive>> primitives;
    LinearBVHNode *nodes = nullptr;
};
```

本类的构造函数会接收一个表示了不同的 BVH 构建方式的参数。在枚举类中，后两者是简单地在结构中点划分或者分为等量的两组，其性能都不算高，而前两者则会在接下来详细说明。

## 4.3.1 BVH 的构建

BVH 的构建一般分为三步：

1. 每一个物体的包围盒会被计算并放在一个数组中
2. 使用传入的 `splitMethod` 参数指向的算法构建二叉树
3. 将二叉树转化为更加紧凑、高效的无指针表示

为了构建 BVH ，需要记录的除了包围盒以外还有包围盒的中心和索引。

```cpp
struct BVHPrimitiveInfo {
    BVHPrimitiveInfo(size_t primitiveNumber, const Bounds3f &bounds)
        : primitiveNumber(primitiveNumber), bounds(bounds),
          centroid(.5f * bounds.pMin + .5f * bounds.pMax) { }
    size_t primitiveNumber;
    Bounds3f bounds;
    Point3f centroid;
};
```

这些信息将被用于构建二叉树结构。不同的算法使用的构建方式并不相同，这将在接下来说明。在构建时同样完成的还有节点总数的设置，以及另一组指向几何体的指针（顺序与原有的可能有区别，因此需要将其与原有指针交换）。构建完成后，函数会返回申请在 `MemoryArena` 上的二叉树的根节点的指针。此时使用的结构是 `BVHBuildNode` ，其内容如下：

```cpp
struct BVHBuildNode {
    void InitLeaf(int first, int n, const Bounds3f &b) { ... }
    void InitInterior(int axis, BVHBuildNode *c0, BVHBuildNode *c1) { ... }
    Bounds3f bounds;
    BVHBuildNode *children[2];
    int splitAxis, firstPrimOffset, nPrimitives;
};
```

节点中保存最终指向的物体的指针的方式是通过两个整数 `firstPrimOffset, nPrimitives` 分别表示在指针数组中的起始偏移和几何体数目。

<center><img src="https://pbr-book.org/3ed-2018/Primitives_and_Intersection_Acceleration/BVH%20choose%20split%20axis.svg" style="max-height: 40vh; margin: 10px"/></center>

在每个节点内部，分割算法会选择坐标轴中的一个作为基准（PBRT 中选择了坐标范围最广的轴），确定一个分割位置使得该位置两侧的几何体分别属于下一级不同的节点。这个过程持续到该节点中只有一个几何体，或者该节点中所有几何体的中心都一样为止。每产生一个叶子节点，其中的几何体指针就会被增加到初始为空的 `orderedPrims` 的最后以形成一个良好排列的数组。

分割完成后，会根据最终的节点数量申请对应大小的内存保存更加紧凑的结构 `LinearBVHNodes`

```cpp
struct LinearBVHNode {
    Bounds3f bounds;
    union {
        int primitivesOffset;   // leaf
        int secondChildOffset;  // interior
    };
    uint16_t nPrimitives;  // 0 -> interior node
    uint8_t axis;          // interior node: xyz
    uint8_t pad[1];        // ensure 32 byte total size
};
```

## 4.3.2 SAH 分割算法

所有分割算法都试图找到一种分割方式，使得在节点上进行光线相交计算的开销尽可能的少。寻找到一个这样的全局最优解是十分困难的，所以各种算法一般会构建一个 cost 模型来替代估计实际的开销、并使用特定的算法（如贪心算法）找到这个 cost 模型的局部最优解。

SAH 的 cost 模型如下：

设一个聚合中有 $N$ 个几何体，在每个几何体上做相交测试的开销分别是 $t_{isect}(o_i)$ ，则当这个节点是叶子节点时的总开销为：

$$C_l = \sum_{i=1}^N t_{isect(o_i)}$$

如果将这个聚合中的几何体分为 $A,B$ 两部分，使得可以使用 $t_{trav}$ 的时间确定某光线需要继续测试哪些部分的相交情况（概率分别为 $p_A, p_B$ ），则这种中间节点的总开销为：

$$C_i = t_{trav} + p_A\sum_{i=0}^{N_A}t_{isect}(a_i) + p_B\sum_{i=0}^{N_B}t_{isect}(b_i) $$

PBRT 基于构建和使用的效率考量制定了以下的规则：

1. 假设对于所有几何体 $t_{isect}$ 均相等且约等于 $t_{trav}$ 的 8 倍，并使用每个部分的表面积估计 $p_A, p_B$ （这也是 SAH 算法名字的来源）
2. 使用固定数量的（代码中是 12 个）均匀分布的候选位置确定分割方法，而不是尝试所有可能的位置
3. 当节点中几何体的数量小于一定值（代码中是 2 个）时，直接将节点中的几何体分割为两个含有同样数量的叶子节点，因为此时使用 SAH 算法对效率并没有明显提高但又很耗费资源
4. 只有当分割后的 cost 低于分割前时，分割才会发生

<center><img src="https://pbr-book.org/3ed-2018/Primitives_and_Intersection_Acceleration/BVH%20split%20bucketing.svg" style="max-height: 25vh; margin: 10px"/></center>

## 4.3.3 Linear BVH

使用 SAH 构建的 BVH 有两个主要的缺陷：

1. 一个场景中的几何体会被在构建BVH时重复计算很多遍表面积
2. 自上而下的 BVH 构建结构难以并行化

为了解决这些问题，诞生了 LBVH 算法。在 LBVH 中，树的生成时间与几何体数量线性相关、且可以快速生成可供独立并行的分块，提高了其并行程度。其最主要的特点是将 BVH 的构建算法理解为一个排序算法。它使用 Morton Codes 将高维空间中相邻的点映射到在一维的线上的点，从而使他们能够被更容易地排序。当节点被排序完成后，空间上相邻的几何体群在排序后的序列中也相邻。

### Morton Codes

这种编码方式并不复杂，实际上就相当于将高维的点的各个坐标的二进制表示中每个 bit 交叉排列，如点 $(x, y)$ 的 Morton Codes 表示就是 $\cdots y_{b2}x_{b2}y_{b1}x_{b1}y_{b0}x_{b0}$ 

这种编码用一种类似 Z 字分型的方式将空间中的区域连成一维的线，示意图如下：

<center><img src="https://pbr-book.org/3ed-2018/Primitives_and_Intersection_Acceleration/Morton%20Basic.svg" style="max-height: 30vh; margin: 10px"/></center>

### HLBVH

LBVH 就相当于使用简单的等分点分割方法构建的 BVH ，它直接在一个节点的每个坐标轴的坐标范围的等分点处切分平面。这种结构并不足够高效。因此此处引入了新的 HLBVH ，首先使用 LBVH 算法构建底层的子树，接着在这些子树的基础上使用 SAH 算法构建更高效的 BVH 结构

<center><img src="https://pbr-book.org/3ed-2018/Primitives_and_Intersection_Acceleration/LBVH%20treelet%20clusters.svg" style="max-height: 25vh; margin: 10px"/></center>

HLBVH 的构建流程如下：

1. 首先计算所有几何体的最大的包围盒和每个几何体对应的 Morton Codes 
2. 使用基数排序算法排序所有节点，每个排序轮次会使用多位
3. 在得到排序后的几何体后，将临近区域内的几何体首先聚合称为底层的子树，子树的生成规则如下：
   1. 每个子树中的节点应该都在同一块相邻的区域中（判断方法是 mask 后的 morton code 一致，即表示基层的一部分 code 相同）
   2. 每个子树中的节点数量应该小于某个值，当数量过多的时候会修改 mask 尝试更加细的分解
   3. 当 mask 缩小到最小时，所有节点都在一个 morton code 表示的最小单元格中，此时无条件生成一个子树
   4. 在串行完成第一部分粗分类后，后两部分将会并行地完成
4. 将生成的子树列表视作几何体列表使用 SAH 构建上层的 BVH

## 4.3.4 BVH 的压缩和索引优化

BVH 构建完成后需要被进一步优化为更加缓存和内存高效的模块。最终的 BVH 会被使用深度优先的算法展开并储存在一段连续的线性内存空间中。这种结构让每个节点的第一个子节点都必定是该节点的直接后继，因此只需要显式储存指向第二个子节点的偏移量即可。

<center><img src="https://pbr-book.org/3ed-2018/Primitives_and_Intersection_Acceleration/BVH%20linearization.svg" style="max-height: 20vh; margin: 10px"/></center>

```cpp
struct LinearBVHNode {
    Bounds3f bounds;
    union {
        int primitivesOffset;    // leaf
        int secondChildOffset;   // interior
    };
    uint16_t nPrimitives;  // 0 -> interior node
    uint8_t axis;          // interior node: xyz
    uint8_t pad[1];        // ensure 32 byte total size
};
```

整个结构体恰好只有 32 bytes ，使得它可以被直接放入一个 cache line 中优化缓存性能。

整个线性结构的构建是一个精心设计的深度优先遍历算法：

```cpp
int BVHAccel::flattenBVHTree(BVHBuildNode *node, int *offset) {
    LinearBVHNode *linearNode = &nodes[*offset];
    linearNode->bounds = node->bounds;
    int myOffset = (*offset)++;
    if (node->nPrimitives > 0) {
        CHECK(!node->children[0] && !node->children[1]);
        CHECK_LT(node->nPrimitives, 65536);
        linearNode->primitivesOffset = node->firstPrimOffset;
        linearNode->nPrimitives = node->nPrimitives;
    } else {
        // Create interior flattened BVH node
        linearNode->axis = node->splitAxis;
        linearNode->nPrimitives = 0;
        flattenBVHTree(node->children[0], offset);
        linearNode->secondChildOffset =
            flattenBVHTree(node->children[1], offset);
    }
    return myOffset;
}
```

## 4.3.5 BVH 的遍历

遍历 BVH 相当于递归地向下搜索中间节点，通过在途中抛弃与包围盒不会相交的节点提升效率。最后返回在叶子节点处调用几何体相交函数的结果。算法使用一个栈维护接下来需要访问的节点。

```cpp
bool BVHAccel::Intersect(const Ray &ray, SurfaceInteraction *isect) const {
    if (!nodes) return false;
    ProfilePhase p(Prof::AccelIntersect);
    bool hit = false;
    Vector3f invDir(1 / ray.d.x, 1 / ray.d.y, 1 / ray.d.z);
    int dirIsNeg[3] = {invDir.x < 0, invDir.y < 0, invDir.z < 0};
    // Follow ray through BVH nodes to find primitive intersections
    int toVisitOffset = 0, currentNodeIndex = 0;
    int nodesToVisit[64];
    while (true) {
        const LinearBVHNode *node = &nodes[currentNodeIndex];
        // Check ray against BVH node
        if (node->bounds.IntersectP(ray, invDir, dirIsNeg)) {
            if (node->nPrimitives > 0) {
                // Intersect ray with primitives in leaf BVH node
                for (int i = 0; i < node->nPrimitives; ++i)
                    if (primitives[node->primitivesOffset + i]->Intersect(
                            ray, isect))
                        hit = true;
                if (toVisitOffset == 0) break;
                currentNodeIndex = nodesToVisit[--toVisitOffset];
            } else {
                // Put far BVH node on _nodesToVisit_ stack, advance to near
                // node
                if (dirIsNeg[node->axis]) {
                    nodesToVisit[toVisitOffset++] = currentNodeIndex + 1;
                    currentNodeIndex = node->secondChildOffset;
                } else {
                    nodesToVisit[toVisitOffset++] = node->secondChildOffset;
                    currentNodeIndex = currentNodeIndex + 1;
                }
            }
        } else {
            if (toVisitOffset == 0) break;
            currentNodeIndex = nodesToVisit[--toVisitOffset];
        }
    }
    return hit;
}
```

# 4.4 Kd-Tree Accelerator

Binary space partitioning (BSP) 的算法通过平面适应性地分割空间。与 BVH 不同的是，这种算法并不会产生重叠的区域，而是在几何体刚好落在分割线中间时将它同时加入两侧计算。这种算法会递归地进行，直到节点中的几何体数量足够小或者树足够深的时候停止计算。因为分隔平面可以任意地放置在空间的任何位置，而三维空间的不同位置可以得到不同程度的细化，BSP 可以有效地适应场景中几何体分布不均匀的情况。

BSP 算法的两种变体分别是 kd-tree 和 octrees 。kd-tree 简单地限制分割平面必须垂直于某个坐标轴，从而加速结构的构建和使用；而 octrees 则在 kd-tree 的基础上，在一次分割中同时使用三个垂直于不同坐标轴的平面将空间分为 8 份。一个简单的 kd-tree 构建过程实例如下：

<center><img src="https://pbr-book.org/3ed-2018/Primitives_and_Intersection_Acceleration/kd%20tree%20splits.svg" style="max-height: 40vh; margin: 10px"/></center>

```cpp
class KdTreeAccel : public Aggregate {
  public:
    KdTreeAccel(std::vector<std::shared_ptr<Primitive>> p,
                int isectCost = 80, int traversalCost = 1,
                Float emptyBonus = 0.5, int maxPrims = 1, int maxDepth = -1);
    Bounds3f WorldBound() const { return bounds; }
    ~KdTreeAccel();
    bool Intersect(const Ray &ray, SurfaceInteraction *isect) const;
    bool IntersectP(const Ray &ray) const;

  private:
    void buildTree(int nodeNum, const Bounds3f &bounds,
                   const std::vector<Bounds3f> &primBounds, int *primNums,
                   int nprims, int depth,
                   const std::unique_ptr<BoundEdge[]> edges[3], int *prims0,
                   int *prims1, int badRefines = 0);

    const int isectCost, traversalCost, maxPrims;
    const Float emptyBonus;
    std::vector<std::shared_ptr<Primitive>> primitives;
    std::vector<int> primitiveIndices;
    KdAccelNode *nodes;
    int nAllocedNodes, nextFreeNode;
    Bounds3f bounds;
};
```

## 4.4.1 树的表示方法

kd-tree 也是一种二叉树，每个节点中均需要储存的信息和 BVH 相似。 kd-tree 的节点被包装为一个只有 8 byte 的结构体：

```cpp
struct KdAccelNode {
	public:
    union {
        Float split;                 // Interior
        int onePrimitive;            // Leaf
        int primitiveIndicesOffset;  // Leaf
    };

  private:
    union {
        int flags;       // Both
        int nPrims;      // Leaf
        int aboveChild;  // Interior
    };
};
```

`KdAccelNode::flags` 的低二位被用于指示该节点的类型（沿 $x,y,z$ 分割的中间节点或叶子节点）。

对于叶子节点，`KdAccelNode::flags` 的高 30 位保存了内含的几何体数量，当仅有一个几何体时，`KdAccelNode::onePrimitive` 保存了该几何体的序号，而当有多个几何体时，则在 `KdTreeAccel::primitiveIndices` 中按照节点编号顺序储存这各个节点之中的几何体编号序列为一个连续数组，并在节点的 `KdAccelNode::primitiveIndicesOffset` 中保存这一节点对应序列的首位偏移。

对于中间节点，`KdAccelNode::split` 保存了分割的位置，而 `KdAccelNode::aboveChild` 的高 30 位则储存了在分割平面上方的子节点的偏移（和 BVH 的压缩方法一样，中间节点的直接后继就是它的其中一个子节点）

## 4.4.2 建树过程

在 `KdTreeAccel`  类中，所有的节点被储存在连续的空间中，使用指针 `node` 可以访问它们。而 `nextFreeNode, nAllocedNode` 则代表了下一个可用的节点的位置和当前已经申请的节点数量。

在开始递归算法前，需要进行以下的初始化步骤：

1. 为了防止树的深度因为某些特殊情况不理智地增长，当最大深度没有被设置时，会使用一个经验值 $8 + 1.3\log_2 N$ 来设置最大深度。
2. 预计算每个几何体的包围盒和所有几何体的包围盒，前者被预先放置在一个列表中
3. 初始化储存节点中的几何体序号的连续数组。由于根节点中一定包含了所有节点，因此其初始值为 0 ~ N - 1 的所有整数
4. 提前申请各种内存，如储存节点序列的内存块和暂存分割位置的信息结构体等

递归的建树流程如下：

1. 找到下一个未使用但已申请的节点位置，如果已经用完了已申请的内存，则重新申请一个容量是原来的两倍的内存空间，并将原有内容迁移至新空间中
2. 当达到了深度限制或者几何体的数量足够少后，生成一个叶子节点终止递归
3. 如果这是一个内部节点，使用类似 SAH 的算法找到分割轴向和分割位置

kd-tree 中使用的分割算法与 SAH 有几点不同：

1. kd-tree 中是有可能分割出不含任何几何体的子区域的。这种空区域不需要任何操作就可以被直接跳过，因此会在 cost 模型中产生一个额外的 bonus 项（下式中的 $b_e$ 只在存在空区域时非零）：

   $$C = t_{trav} + (1-b_e)(p_BN_B + p_AN_A)t_{isect}$$

   > 这块没看懂为什么要引入这个 $b_e$

2. 通过扫描每个物体的包围盒的六条边可以找到最合适的分割位置，这六条边被保存在 `BoundEdge` 结构中。易知，在每个轴向上之多需要 $2N$ 个这样的结构，这些内存会被事先申请并在各层之间复用。

   ```cpp
   enum class EdgeType { Start, End };
   struct BoundEdge {
       Float t;
       int primNum;
       EdgeType type;
   };
   ```

   在计算完将该节点直接作为叶子节点的 cost 后，函数会遍历所有该节点内的节点生成 `BoundEdge` 、然后在该轴向上排序所有边，最后从小到大地依次计算在每个位置上的 cost 。

   算法会首先在空间最大的轴向上尝试分割，如果找不到比直接生成叶子节点更好的分割方法的话还会尝试另外两个轴向。特别地，在一些极端情况下可能根本找不到有用的分割方法（如下图），此时就只能放弃分割而生成一个叶子节点。

   <center><img src="https://pbr-book.org/3ed-2018/Primitives_and_Intersection_Acceleration/Overlapping%20bboxes.svg" style="max-height: 25vh; margin: 10px"/></center>

3. 在确认了分割位置后，可以简单地将各个分割后的几何体指针分配到传入子节点的数组 `prims0, prims1` 内。因为会直接创建左子节点，前者可以直接传入接下来的子树中，而后者需要保存到左子树完成后才被第二次的建树操作使用：

   ```cpp
   // Recursively initialize children nodes
   Float tSplit = edges[bestAxis][bestOffset].t;
   Bounds3f bounds0 = nodeBounds, bounds1 = nodeBounds;
   bounds0.pMax[bestAxis] = bounds1.pMin[bestAxis] = tSplit;
   buildTree(nodeNum + 1, bounds0, allPrimBounds, prims0, n0, depth - 1, edges,
             prims0, prims1 + nPrimitives, badRefines);
   int aboveChild = nextFreeNode;
   nodes[nodeNum].InitInterior(bestAxis, aboveChild, tSplit);
   buildTree(aboveChild, bounds1, allPrimBounds, prims1, n1, depth - 1, edges,
             prims0, prims1 + nPrimitives, badRefines);
   ```

## 4.4.3 kd-tree 的遍历

<center><img src="https://pbr-book.org/3ed-2018/Primitives_and_Intersection_Acceleration/kd%20ray%20traversal.svg" style="max-height: 40vh; margin: 10px"/></center>

上图展示了 kd-tree 遍历的流程，在与当前中间节点的区域相交后，会计算出相交的 `tmin, tmax, tsplit` 。向子节点的遍历会从靠近 `tmin` 的更近的那个子节点先开始，直到抵达叶子节点后遍历其中的所有几何体。只有当在近子节点报告了不相交后远子节点才会被遍历到，如果不存在远子节点（`tsplit >= tmx`）则直接报告不相交。这种 dfs 实际上使用了一个保存待访问节点信息的栈来实现：

```cpp
struct KdToDo {
    const KdAccelNode *node;
    Float tMin, tMax;
};
```

近节点会被直接遍历，而后一个需要遍历的节点将被压入栈中