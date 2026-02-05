using Avalonia.Data.Converters;
using Avalonia.Markup.Xaml;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Silk.NET.OpenGL;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace Avalonia3D.Controls
{
    public class Vector3Extension : MarkupExtension
    {
        public string Values { get; set; }

        public Vector3Extension(string values)
        {
            Values = values;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return Model3DControl.ParseVector3(Values);
        }
    }
   
    public class OpenGl3DControl : OpenGlControlBase
    {
        private GL? _gl;
        private uint _shaderProgram;
        private uint _vao;
        private uint _vbo;
        private uint _texture;
        private float _rotationAngle;
        private Matrix4x4 _projection;
        private Matrix4x4 _view = Matrix4x4.CreateLookAt(
            new Vector3(0, 0, 3),
            Vector3.Zero,
            Vector3.UnitY);

        // Параметры управления
        public float RotationSpeed { get; set; } = 1.0f;
        public Vector3 LightPosition { get; set; } = new Vector3(2.0f, 2.0f, 2.0f);

        protected override void OnOpenGlInit(GlInterface gl)
        {
            base.OnOpenGlInit(gl);

            try
            {
                // Инициализация OpenGL
                _gl = GL.GetApi(gl.GetProcAddress);

                // Проверка версии OpenGL ES
                string version = _gl.GetStringS(StringName.Version);
                Console.WriteLine($"OpenGL version: {version}");

                if (!version.StartsWith("OpenGL ES 3"))
                {
                    Console.WriteLine("WARNING: OpenGL ES 3.0 not available, fallback may not work");
                }

                // Создание шейдерной программы
                _shaderProgram = CreateShaderProgram();

                // Создание геометрии куба
                CreateCubeGeometry();

                // Создание текстуры
                _texture = CreateTexture();

                // Настройка OpenGL
                _gl.Enable(EnableCap.DepthTest);
                _gl.DepthFunc(DepthFunction.Less);
                _gl.ClearColor(0.1f, 0.1f, 0.2f, 1.0f);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OpenGL initialization failed: {ex}");
                throw;
            }
        }

        protected override void OnOpenGlRender(GlInterface gl, int fb)
        {
            if (_gl == null) return;

            int width = (int)Bounds.Width;
            int height = (int)Bounds.Height;

            if (width <= 0 || height <= 0)
                return;

            // Установка области вывода
            _gl.Viewport(0, 0, (uint)width, (uint)height);

            // Обновление матрицы проекции
            _projection = Matrix4x4.CreatePerspectiveFieldOfView(
                MathF.PI / 4,
                (float)width / height,
                0.1f,
                100f);

            // Очистка буферов
            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            // Плавное вращение
            _rotationAngle += 0.01f * RotationSpeed;

            // Матрица модели (вращение + масштаб)
            var model = Matrix4x4.CreateScale(0.8f)
                * Matrix4x4.CreateRotationX(_rotationAngle * 0.5f)
                * Matrix4x4.CreateRotationY(_rotationAngle);

            // Расчет MVP-матрицы
            var mvp = model * _view * _projection;

            // Активация шейдерной программы
            _gl.UseProgram(_shaderProgram);           

            // Запрос следующего кадра
            RequestNextFrameRendering();
        }

        protected override void OnOpenGlDeinit(GlInterface gl)
        {
            // Освобождение ресурсов
            if (_gl != null)
            {
                _gl.DeleteVertexArray(_vao);
                _gl.DeleteBuffer(_vbo);
                _gl.DeleteProgram(_shaderProgram);
                _gl.DeleteTexture(_texture);
                _gl = null;
            }

            base.OnOpenGlDeinit(gl);
        }

        #region Shader Program
        private uint CreateShaderProgram()
        {
            string vertSource, fragSource;
            string version = _gl.GetStringS(StringName.Version);
            if (version.Contains("OpenGL ES 3"))
            {
                // Вершинный шейдер
                vertSource =
                "#version 300 es\n" +
                "precision highp float;\n" +
                "layout(location = 0) in vec3 aPosition;\n" +
                "layout(location = 1) in vec3 aNormal;\n" +
                "layout(location = 2) in vec2 aTexCoord;\n" +
                "uniform mat4 uMVP;\n" +
                "uniform mat4 uModel;\n" +
                "out vec3 FragPos;\n" +
                "out vec3 Normal;\n" +
                "out vec2 TexCoord;\n" +
                "void main()\n" +
                "{\n" +
                "    gl_Position = uMVP * vec4(aPosition, 1.0);\n" +
                "    FragPos = vec3(uModel * vec4(aPosition, 1.0));\n" +
                "    Normal = mat3(transpose(inverse(uModel))) * aNormal;\n" +
                "    TexCoord = aTexCoord;\n" +
                "}";

                // Фрагментный шейдер
                fragSource =
                   "#version 300 es\n" +
                   "precision highp float;\n" +
                   "in vec3 FragPos;\n" +
                   "in vec3 Normal;\n" +
                   "in vec2 TexCoord;\n" +
                   "out vec4 FragColor;\n" +
                   "uniform sampler2D uTexture;\n" +
                   "uniform vec3 uLightPos;\n" +
                   "uniform vec3 uViewPos;\n" +
                   "void main()\n" +
                   "{\n" +
                   "    // Ambient\n" +
                   "    float ambientStrength = 0.2;\n" +
                   "    vec3 ambient = ambientStrength * vec3(1.0, 1.0, 1.0);\n" +
                   "    \n" +
                   "    // Diffuse\n" +
                   "    vec3 norm = normalize(Normal);\n" +
                   "    vec3 lightDir = normalize(uLightPos - FragPos);\n" +
                   "    float diff = max(dot(norm, lightDir), 0.0);\n" +
                   "    vec3 diffuse = diff * vec3(1.0, 1.0, 1.0);\n" +
                   "    \n" +
                   "    // Specular\n" +
                   "    float specularStrength = 0.5;\n" +
                   "    vec3 viewDir = normalize(uViewPos - FragPos);\n" +
                   "    vec3 reflectDir = reflect(-lightDir, norm);\n" +
                   "    float spec = pow(max(dot(viewDir, reflectDir), 0.0), 32.0);\n" +
                   "    vec3 specular = specularStrength * spec * vec3(1.0, 1.0, 1.0);\n" +
                   "    \n" +
                   "    // Combine\n" +
                   "    vec4 texColor = texture(uTexture, TexCoord);\n" +
                   "    vec3 result = (ambient + diffuse + specular) * texColor.rgb;\n" +
                   "    FragColor = vec4(result, 1.0);\n" +
                   "}";
            }
            else
            {
                // Fallback для OpenGL ES 2.0
                vertSource = @"#version 100
            attribute vec3 aPosition;
            attribute vec3 aNormal;
            
            uniform mat4 uMVP;
            uniform mat4 uModel;
            
            varying vec3 FragPos;
            varying vec3 Normal;
            
            void main()
            {
                gl_Position = uMVP * vec4(aPosition, 1.0);
                FragPos = vec3(uModel * vec4(aPosition, 1.0));
                Normal = mat3(transpose(inverse(uModel))) * aNormal;
            }";

                fragSource = @"#version 100
            precision mediump float;
            
            varying vec3 FragPos;
            varying vec3 Normal;
            
            uniform vec3 uObjectColor;
            uniform vec3 uLightPos;
            uniform vec3 uViewPos;
            uniform float uAmbientStrength;
            uniform float uSpecularStrength;
            uniform int uShininess;
            
            void main()
            {
                // Ambient
                vec3 ambient = uAmbientStrength * vec3(1.0, 1.0, 1.0);
                
                // Diffuse 
                vec3 norm = normalize(Normal);
                vec3 lightDir = normalize(uLightPos - FragPos);
                float diff = max(dot(norm, lightDir), 0.0);
                vec3 diffuse = diff * vec3(1.0, 1.0, 1.0);
                
                // Specular
                vec3 viewDir = normalize(uViewPos - FragPos);
                vec3 reflectDir = reflect(-lightDir, norm);
                float spec = pow(max(dot(viewDir, reflectDir), 0.0), float(uShininess));
                vec3 specular = uSpecularStrength * spec * vec3(1.0, 1.0, 1.0);
                
                vec3 result = (ambient + diffuse + specular) * uObjectColor;
                gl_FragColor = vec4(result, 1.0);
            }";
            }
            // Создание шейдеров
            uint vertexShader = CompileShader(ShaderType.VertexShader, vertSource);
            uint fragmentShader = CompileShader(ShaderType.FragmentShader, fragSource);

            // Создание программы
            uint program = _gl!.CreateProgram();
            _gl.AttachShader(program, vertexShader);
            _gl.AttachShader(program, fragmentShader);
            _gl.LinkProgram(program);

            // Проверка ошибок линковки
            string programLog = _gl.GetProgramInfoLog(program);
            if (!string.IsNullOrEmpty(programLog))
                Console.WriteLine($"Program link log: {programLog}");

            _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int status);
            if (status == 0)
                throw new Exception("Program linking failed");

            // Удаление шейдеров (больше не нужны)
            _gl.DeleteShader(vertexShader);
            _gl.DeleteShader(fragmentShader);

            return program;
        }

        private uint CompileShader(ShaderType type, string source)
        {
            uint shader = _gl!.CreateShader(type);
            _gl.ShaderSource(shader, source);
            _gl.CompileShader(shader);

            // Проверка ошибок компиляции
            string infoLog = _gl.GetShaderInfoLog(shader);
            if (!string.IsNullOrEmpty(infoLog))
                Console.WriteLine($"{type} compile log: {infoLog}");

            _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int status);
            if (status == 0)
                throw new Exception($"{type} compilation failed");

            return shader;
        }
        #endregion

        #region Geometry
        private unsafe void CreateCubeGeometry()
        {
            // Вершины: позиция (3), нормаль (3), текстурные координаты (2)
            float[] vertices = {
                // Передняя грань
                -0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 0.0f,
                 0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 0.0f,
                 0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 1.0f,
                 0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  1.0f, 1.0f,
                -0.5f,  0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 1.0f,
                -0.5f, -0.5f,  0.5f,  0.0f,  0.0f,  1.0f,  0.0f, 0.0f,
                
                // Задняя грань
                -0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 0.0f,
                -0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 0.0f,
                 0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 1.0f,
                 0.5f,  0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  1.0f, 1.0f,
                 0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 1.0f,
                -0.5f, -0.5f, -0.5f,  0.0f,  0.0f, -1.0f,  0.0f, 0.0f,
                
                // Левая грань
                -0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
                -0.5f,  0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 1.0f,
                -0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
                -0.5f, -0.5f, -0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
                -0.5f, -0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  0.0f, 0.0f,
                -0.5f,  0.5f,  0.5f, -1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
                
                // Правая грань
                 0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
                 0.5f,  0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f,
                 0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
                 0.5f, -0.5f, -0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 1.0f,
                 0.5f, -0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  0.0f, 0.0f,
                 0.5f,  0.5f,  0.5f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f,
                
                // Нижняя грань
                -0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 1.0f,
                 0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 1.0f,
                 0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 0.0f,
                 0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  1.0f, 0.0f,
                -0.5f, -0.5f,  0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 0.0f,
                -0.5f, -0.5f, -0.5f,  0.0f, -1.0f,  0.0f,  0.0f, 1.0f,
                
                // Верхняя грань
                -0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 1.0f,
                 0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 1.0f,
                 0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 0.0f,
                 0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  1.0f, 0.0f,
                -0.5f,  0.5f,  0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 0.0f,
                -0.5f,  0.5f, -0.5f,  0.0f,  1.0f,  0.0f,  0.0f, 1.0f
            };

            // Создание VAO и VBO
            _vao = _gl!.GenVertexArray();
            _vbo = _gl.GenBuffer();

            _gl.BindVertexArray(_vao);
            _gl.BindBuffer(GLEnum.ArrayBuffer, _vbo);

            // Загрузка данных вершин
            unsafe
            {
                fixed (float* ptr = vertices)
                {
                    _gl.BufferData(
                        GLEnum.ArrayBuffer,
                        (nuint)(vertices.Length * sizeof(float)),
                        ptr,
                        GLEnum.StaticDraw);
                }
            }

            // Установка атрибутов вершин
            // Позиция
            _gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)0);
            _gl.EnableVertexAttribArray(0);

            // Нормаль
            _gl.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)(3 * sizeof(float)));
            _gl.EnableVertexAttribArray(1);

            // Текстурные координаты
            _gl.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 8 * sizeof(float), (void*)(6 * sizeof(float)));
            _gl.EnableVertexAttribArray(2);

            // Отвязка
            _gl.BindBuffer(GLEnum.ArrayBuffer, 0);
            _gl.BindVertexArray(0);
        }
        #endregion

        #region Texture
        private uint CreateTexture()
        {
            // Создаем текстуру с шахматным узором
            const int size = 64;
            byte[] pixels = new byte[size * size * 4];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int index = (y * size + x) * 4;
                    bool isDark = ((x / 8) + (y / 8)) % 2 == 1;

                    // Оранжевый и темно-оранжевый
                    pixels[index + 0] = isDark ? (byte)200 : (byte)255; // R
                    pixels[index + 1] = isDark ? (byte)80 : (byte)140; // G
                    pixels[index + 2] = isDark ? (byte)20 : (byte)40;  // B
                    pixels[index + 3] = 255;                           // A
                }
            }

            // Создание текстуры
            uint texture = _gl!.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, texture);

            // Загрузка данных текстуры
            unsafe
            {
                fixed (byte* ptr = pixels)
                {
                    _gl.TexImage2D(
                        TextureTarget.Texture2D,
                        0,
                        InternalFormat.Rgba,
                        (uint)size,
                        (uint)size,
                        0,
                        GLEnum.Rgba,
                        PixelType.UnsignedByte,
                        ptr);
                }
            }

            // Настройка параметров текстуры
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            // Генерация мипмапов
            _gl.GenerateMipmap(TextureTarget.Texture2D);

            return texture;
        }
        #endregion

        // Для управления снаружи
        public void SetCameraPosition(Vector3 position, Vector3 target)
        {
            _view = Matrix4x4.CreateLookAt(position, target, Vector3.UnitY);
            RequestNextFrameRendering();
        }

        public void SetBackgroundColor(float r, float g, float b)
        {
            if (_gl != null)
            {
                _gl.ClearColor(r, g, b, 1.0f);
                RequestNextFrameRendering();
            }
        }
    }
}