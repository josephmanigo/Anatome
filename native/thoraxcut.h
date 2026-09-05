#pragma once
#ifdef _WIN32
#define TL_API extern "C" __declspec(dllexport)
#else
#define TL_API extern "C" __attribute__((visibility("default")))
#endif
// Vertex layout: xyz, uv, interior flag. Pose layout: 8 xyz blade-box corners.
// Caller retains its input. Result owns all output until tl_free.
TL_API void* tl_cut(const float* vertices, int vertexCount, const int* triangles,
                   int indexCount, const float* bladeCorners, int poseCount,
                   int maxTriangles, int maxPieces);
TL_API int tl_status(void* result);
TL_API const char* tl_message(void* result);
TL_API int tl_piece_count(void* result);
TL_API int tl_piece_sizes(void* result, int piece, int* vertices, int* indices);
TL_API int tl_piece_copy(void* result, int piece, float* vertices, int vertexFloats,
                         int* indices, int indexCount);
TL_API void tl_free(void* result);
TL_API const char* tl_version();
TL_API void* tl_normalize(const float* vertices,int vertexCount,const int* triangles,int indexCount);
TL_API void* tl_resolve_compound(const float* vertices,int vertexCount,const int* triangles,int indexCount);
