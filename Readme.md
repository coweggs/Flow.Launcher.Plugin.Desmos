# Flow Launcher Desmos Plugin

A Flow Launcher plugin for quickly previewing mathematical expressions with
[Desmos](https://www.desmos.com) from the search bar.

<div align="center">
  <img src="https://github.com/user-attachments/assets/a3ef009a-63a4-401b-b161-46f8f164ac32" width="400"/>
  <img src="https://github.com/user-attachments/assets/46187217-af6e-4389-b4d7-839997126ea6" width="400"/>
</div>

## Installation
    pm install https://github.com/coweggs/Flow.Launcher.Plugin.Desmos/releases/download/1.0.0/Desmos.zip

## Usage

Use the `des` keyword, and preview pane (F1) to show the interface.

```text
des y=x^2
des y=sin(x)
des y=x^2 ; y=2x+1
des 3d z=sin(x)*cos(y)
```

Separate multiple expressions with `;`. Expressions are rendered in an inline
Desmos preview, and pressing Enter copies the original input to the clipboard.

## Supported functionality

### Graph modes

- 2D graphs are the default.
- Add `3d` before the expression for a 3D graph:
  `des 3d z=sin(x)*cos(y)`.
- The preview uses Desmos's calculator and refreshes when the query changes.
- Desmos theme can follow the Flow Launcher theme when theme synchronization
  is enabled in settings.

### Expression conversion

Normal Desmos and LaTeX syntax is supported. The plugin also converts common
plain-text forms before displaying the preview:

| Input | Converted form |
| --- | --- |
| `sqrt(x)` | `\sqrt{x}` |
| `cbrt(x)` | `\sqrt[3]{x}` |
| `root(n,x)` or `nthroot(n,x)` | `\sqrt[n]{x}` |
| `x^100` | `x^{100}` |
| `x^(1/3)` | `x^{1/3}` |
| `pi` | `\pi` |
| `infinity` or `inf` | `\infty` |
| `abs(x)` | `\left|x\right|` |
| `|x|` | `\left|x\right|` |
| `theta` | `\theta` |
| `*` | `\cdot` |

`root` and `nthroot` use the order `root(index,value)`, for example
`root(3,8)`.

Absolute-value notation can be used either as `abs(x)` or `|x|`.

### Function names and aliases

Function names are converted when followed by parentheses. Supported
function groups include:

- Trigonometric: `sin`, `cos`, `tan`, `sec`, `csc`, `cot`
- Inverse trigonometric: `arcsin`, `arccos`, `arctan`
- Hyperbolic: `sinh`, `cosh`, `tanh`
- Logarithms: `ln`, `log`
- Rounding and comparison: `floor`, `ceil`, `round`, `sign`, `min`, `max`,
  `mod`

Aliases are converted as follows:

| Aliases | Canonical function |
| --- | --- |
| `asin` | `arcsin` |
| `acos` | `arccos` |
| `atan` | `arctan` |
| `cosec`, `cosecant` | `csc` |
| `cotan`, `cotangent` | `cot` |
| `ceiling` | `ceil` |
| `sgn` | `sign` |

Additional logarithm helpers:

- `log10(x)` and `lg(x)` become base-10 logarithms.
- `log2(x)` becomes a base-2 logarithm.
- `logb(base,value)` becomes a logarithm with the specified base.

The `clamp(value,min,max)` helper is converted to
`min(max(value,min),max)`.

Desmos-native notation can also be entered directly for features such as
piecewise expressions, derivatives, integrals, lists, restrictions, and
parametric or polar graphs.
