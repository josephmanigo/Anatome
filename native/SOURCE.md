# Manifold pin

Repository: https://github.com/elalish/manifold
Release: v3.5.2
Revision: 11235e6b8ebea2dbed8aec4285685aafd3d95667
License: Apache-2.0 (retain vendor LICENSE in distributed notices).

Restore with `git clone --depth 1 --branch v3.5.2 https://github.com/elalish/manifold.git native/vendor/manifold`.
Build with `pipeline/build-native.ps1` and `pipeline/build-native.ps1 -Android`.
Serial Manifold backend; cutting itself runs on a background Unity task. Windows and Android use the same C ABI and six-float vertex layout. Fixture tests use synthetic geometry solely to test the geometry engine; they are not anatomical assets.
