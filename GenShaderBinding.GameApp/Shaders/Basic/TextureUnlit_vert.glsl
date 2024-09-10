#version 100

attribute vec4 a_VertexPosition;
attribute vec2 a_VertexTextureCoord;

varying mediump vec2 v_TextureCoord;

void main(void)
{
    gl_Position = a_VertexPosition;
    v_TextureCoord = a_VertexTextureCoord;
}
