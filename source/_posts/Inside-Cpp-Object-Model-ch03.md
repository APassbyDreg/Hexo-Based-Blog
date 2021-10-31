---
title: 《深度探索C++对象模型》第三章笔记 | Notes for Inside Cpp Object Model Chapter 03
date: 2021-10-26 17:27:21
categories:
- programming
- reading notes
tags:
- C++
- OOP
- 深度探索C++对象模型
toc: true
---

[[toc]]

在这一章中，类的 data members 和 class hierarchy 是中心议题。一个类的 data members 可以表现这个类在运行中的某种状态，而 data members 的 static 与否则体现了这个状态是被单个实例使用还是所有的该类的实例所使用。

# 3.1 Data Members 的绑定

在 C++ 最早的编译器上，变量定义的位置会影响事实上获取的值。考虑如下代码：

```cpp
extern int v;
class Sample
{
public:
    // 注意：在早期 C++ 编译器中，这个函数会返回外部的 v
    int f() { return v; }
private:
    int v;
};
// 近代 C++ 编译器会在这个位置分析函数实体，最后返回类内的 v
```

虽然这个情况在 C++ 2.0 后消失了（现代 C++ 编译器会按照语义返回类对象的变量值），这个古老的语言规则称为 member rewriting rule ，内容大意是：一个函数实体在整个 class 的声明结束前不会被分析。

然而，对于 member function 中的参数列表却并非如此，参数列表中的内容在可分析的情况下是就地 resolve 的，例如下面的代码：

```cpp
typedef float l;
class Sample
{
public:
		// 注意：这里的传入值是一个浮点数，而不是下面定义的 long long
    void test(l _a)
    {
        a = _a;
        cout << _a << endl; // 因此这个位置会输出一个浮点值
    };
    typedef long long l; // 这里的 typedef 并不会被上文看到
    l a; // 这是一个 long long 类型的数据
};
```

# 3.2 Data Members 的布局

C++ Standard 要求：在同一个 access section（即 `public, private, protected` ）中，较晚出现的变量在内存中有较高的地址。然而，标准并不要求各个变量一定要连续排列，也没有要求在不同的 access section 之间的变量需要有明确的先后关系。

大多数编译器选择把多个 access section 合并在一起，并按照声明的次序形成一个连续区块。access sections 的数量并不会引起额外负担。

# 3.3 Data Member 的存取

## 3.3.1 Static Data Members

所有 static 变量被编译器提出于类外，被视为只在该类的生命范围内可见的全局变量。每个 static data member 只有一个实体，它被存放于程序的 data segment 中。每次程序操作该变量，编译器会将其转化为对该唯一的 extern 实体的直接操作。

无论是从 class object 使用 member selection operator（即 `.` 运算符）、还是从一个复杂的派生类中取用、或者从函数的返回值取用，每一个这种变量的使用并不会导致任何时间或者空间上的额外负担。

## 3.3.2 Nonstatic Data Members

这些变量直接存放在每个 class object 中，必须通过 class object 才能对它们进行操作。编译器会将 class object 的起始地址加上该变量的偏移量操作。即：

```cpp
class Sample {};
Sample s;
&s.x == &s + (&Sample::_x - 1);
```

类变量的偏移量总被加上 1 ，这样能帮助编译系统区分出“一个指向  data member 的指针，用以指出 class 的第一个 member ” 和 “ 一个指向 data member 的指针，没有指出任何 member ” 这两种情况（这在 3.3.6 中有进一步讨论）。

对于确认变量类型的 class object 的 data member 操作，在任意情况下均不会产生任何额外开销，因为对确定类型而言所有的 offset 均可在编译器确定。

在使用指针或者引用实现多态的情况，对于来自 struct member 、单一  class member 、单继承、多继承的 data members，这些偏移量也可以在编译期间获知。而对于使用了 virtual base class 虚拟继承而来的变量，确认 offset 的过程必须延迟到执行期才能解决。

# 3.4 继承与 Data Members

在 C++ 继承模型中，一个派生类对象所表现出来的东西是其自己加上其基类的总和。但 derived class members 和 base class members 的排列次序并无规定。在大部分编译器上，基类的变量总是先出现，但一旦碰上 virtual base class 就说不准了。

本节讨论了不同情况下的 data members 的排列情况

## 3.4.1 只有继承，没有多态

这种不含多态的情况称为具体继承（ concrete inheritance ，与虚继承相对）。一般而言，这种情况并不会增加额外开销。然而，需要注意的是这种写法可能带来一些问题：

1. 容易重复设计出一些作用相同的函数
2. 由于要求继承下来的类的完整性，可能累加不必要的 padding 从而带来额外的空间开销

## 3.4.2 含有多态的继承

一旦引入多态，势必要增加额外开销，包括了：

1. 增加 vtbl 用于存储对应类型的虚函数入口
2. 在 class object 中增加 vptr 指向 vtbl 
3. 在执行期需要通过跳转才能访问函数入口
4. 修改对应的 constructor 和 deconstructor 设定和删除 vptr 的值

在引入了 vptr 后，一个重要的问题是应该把它放在什么位置：

早期的编译器中，它被放置于 class object 的尾端。这样可以保留 base class 的 C struct 布局，从而允许对象在 C 代码中继续使用。而随着虚继承和抽象基类的引入、加上 OO 编程方式的兴起，一些编译器开始将 vptr 置于 class object 的头部。这对于在多继承下通过指向 class members 的指针调用 virtual function 带来帮助（比如可以合并第一个基类和派生类的 vptr ，让它们指向一个同样的虚函数表，只不过不同指针的访问范围不同）。这两种布局的示意图如下：

- vptr 在尾部：继承了一个含有虚函数的类

<center><img src="vptr-at-the-back.png" style="max-height: 30vh; margin: 10px;"/></center>

- vptr 在头部：继承自一个不含虚函数的类，但类中包含虚函数

<center><img src="vptr-at-the-front.png" style="max-height: 30vh; margin: 10px;"/></center>

特别地，以 g++ 在 c++11 标准下的编译结果为例，内存布局可能是这样的：

<center><img src="https://pic4.zhimg.com/80/v2-886b7c232a54024aa2a211a3e4f2f0bf.jpg" style="max-height: 30vh; margin: 10px;"/></center>

## 3.4.3 多继承

在将虚指针置于尾部时，单继承的派生类和基类的起始地址一样·，这是一种「自然」的继承情况（其实在将指针置于头部时对于大多数编译器也满足这一情况）。

多继承的复杂度在于派生类和其上一个的基类，乃至上上个基类之间都很有可能出现不自然的情况。编译器需要介入设定在类型转换时需要使用的地址偏移。

一般而言，基类在派生类中的排布是连续且遵循声明顺序的。对于一个多重派生对象，其第一个基类的 subobject 的指针仍然等价于派生类本身的指针，而后续的基类的指针则需要加上前面的类型 subobjects 的大小。

一种多重继承的示意图如下：

<center><img src="multi-inheritance-example.png" style="max-height: 40vh; margin: 10px;"/></center>

## 3.4.4 虚拟继承

多重继承在语义上的副作用在于它必须支持某种形式的 shared subobject 继承，一个典型的例子是最早的 iostream 库：

```cpp
class ios { ... };
class istream : public virtual ios { ... };
class ostream : public virtual ios { ... };
class iostream : public istream, public ostream { ... };
```

一般的实现方法是：当一个类内如果含有一个或者多个 virtual base class objects，它将被分割为两个部分：不变部分与共享部分。不变部分的数据总是具有固定的 offset 可以直接操作，而共享部分则通过由各个编译器实现的方式间接操作。

最基础的实现方法是在每个派生类中插入一些指向 virtual base class object 的指针，但这种方法有两个缺点：

1. 每个对象因为虚拟继承带来的额外开销不是固定的
2. 虚拟继承链的加长会导致间接存储层次的增加，而不是固定的额外开销

对于第二个问题，大部分编译器都通过在编译器取得所有的 nested virtual base class 指针放到派生类中，从而解决固定存储时间的问题。如将  `a.(subobj_b).(subobj_c).var`  转化为 `a.(subobj_b_c).var`  。

对于第一个问题有两种解决方案：

1. MSVC 编译器引入了类似 vtbl 的所谓 virtual base class pointer table 的概念
2. 在 vtbl 中存放 virtual base class offset 。如在 Sun 编译器中，vtbl 可以被正值或者负值索引，其中负值的位置存放 virtual base class offset

因为虚拟继承会产生额外的开销，所以对于它而言最有效的运用方式是使用一个抽象的 virtual base class，而其中没有任何 data member

# 3.5 对象成员的效率

在没有继承关系的情况下，如果打开优化（这是为了避免出现因为编译器的策略原因出现的扰动），所有对象成员的使用效率是完全一致的。这在可以直接确定数据地址的单一继承下，情况也是一样的。

然而，一旦引入虚拟继承，测试中所有的编译器都歇菜了。哪怕访问的成员是一个可以在编译期间就能确定地址的非多态对象，虽然不开优化的情况下效率变化并不大，但打开优化之后的效率仍然有显著的降低。这可能说明了间接性会严重影响「将运算移向寄存器或缓存执行」的优化能力。

# 3.6 指向 Data Members 的指针

使用语法 `&Class::data_member_name` 可以获取该成员变量在类内的指针（偏移量）。并使用 `class_object_ptr->(*data_member_ptr)`  应用这个指针。

这种偏移量的做法会遇到无法区分 `NULL` 指针和指向偏移量为 0 的 data member 的指针的问题。这要求所有真正的 member offset 的值均被加一，而在使用这个值时需要注意减一后使用。然而，在部分编译器（如 MSVC ）中这个过程被优化了。这意味着如果你打印这些值，你得到的是原始的偏移量而非加一后的结果。

另外一个需要注意的点是这种指针本身也支持多态。指向基类 data member 的指针可以与指向派生类 data member 互相转换，但这因为继承机制的介入需要加入 subobject 的偏移量而变得相当复杂并产生运行时的额外开销。

使用这种指针的效率表现和在不同情况下使用对象成员的效率是一致的。在虚拟继承以外的场景，打开优化后并不会有明显的效率变化，而一旦引入虚拟继承，虽然不优化的情况下间接层数对效率只有小幅度的影响，却对优化的情况下有重大的影响。原因和直接使用对象成员的情况下是一样的。