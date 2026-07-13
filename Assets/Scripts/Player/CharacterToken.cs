using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MonopolyPlanA
{
    public enum TokenAnim { Idle, Walk, Victory, Defeat }

    /// <summary>
    /// Ficha 2D del jugador en el tablero: un plano (quad) con el sprite del
    /// personaje, siempre mirando a la cámara (billboard). Reproduce las
    /// animaciones por frames desde Resources/Anims:
    /// - Idle en reposo · Walk al moverse · Victory al comprar/construir ·
    ///   Defeat al pagar o quebrar (las dos últimas una sola vez y vuelve a Idle).
    /// </summary>
    public class CharacterToken : MonoBehaviour
    {
        const int FrameCount = 8;

        /// <summary>
        /// Altura del LIENZO en mundo por personaje, calibrada con la fracción
        /// real que el cuerpo ocupa en el idle (rey 0.815, mago 0.893,
        /// arquera 0.816, enano 0.801) para que el CUERPO mida: rey y arquera
        /// 1.95, el mago un pelín más (1.97) y el enano 1.50.
        /// </summary>
        static readonly Dictionary<string, float> CanvasWorldHeight = new Dictionary<string, float>
        {
            { "rey", 2.39f }, { "mago", 2.36f }, { "arquera", 2.39f }, { "enano", 1.87f }
        };

        public float WorldHeight =>
            CanvasWorldHeight.TryGetValue(_charName, out var h) ? h : 2.25f;

        /// <summary>Fracción del lienzo que ocupa el cuerpo (medida en el idle).</summary>
        static readonly Dictionary<string, float> BodyFraction = new Dictionary<string, float>
        {
            { "rey", 0.815f }, { "mago", 0.830f }, { "arquera", 0.816f }, { "enano", 0.801f }
        };

        /// <summary>Y local de la coronilla del personaje (los pies están en -0.5).</summary>
        float HeadTopY =>
            -0.5f + WorldHeight * (BodyFraction.TryGetValue(_charName, out var f) ? f : 0.82f);

        /// <summary>Y local del borde superior de la placa del nombre (para apoyar la corona del líder).</summary>
        /// <summary>Posición MUNDIAL del centro de la placa (ya corregida para vista
        /// Top en 3D). La usa la corona del líder para apilarse sobre la etiqueta.</summary>
        public Vector3 PlateWorldCenter
        {
            get { CachePlate(); return _plate != null ? _plate.position : transform.position + Vector3.up * WorldHeight; }
        }
        /// <summary>Alto MUNDIAL de la placa del nombre.</summary>
        public float PlateHeightWorld
        {
            get { CachePlate(); return _plate != null ? _plate.localScale.y : 0.44f; }
        }

        public float PlateTopY
        {
            get
            {
                CachePlate();
                if (_plate != null)
                    return _plate.localPosition.y + _plate.localScale.y / 2f;
                return HeadTopY + 0.55f;
            }
        }

        /// <summary>Duración de cada frame según animación y variante de caminata.</summary>
        float CurrentFrameTime()
        {
            if (_anim == TokenAnim.Walk)
            {
                switch (_walkVariant)
                {
                    case "walk_back":  return 0.17f; // de espaldas, más pausado
                    case "walk_front": return 0.12f;
                    default:           return 0.09f; // lateral, ágil
                }
            }
            return _anim == TokenAnim.Idle ? 0.28f : 0.14f;
        }

        static readonly Dictionary<string, Texture2D[]> Cache = new Dictionary<string, Texture2D[]>();

        /// <summary>
        /// Orientación nativa del arte del walk lateral por personaje:
        /// +1 = el sheet mira a la derecha, -1 = mira a la izquierda.
        /// </summary>
        static readonly Dictionary<string, float> SideArtFacing = new Dictionary<string, float>
        {
            { "rey", -1f }, { "mago", -1f }, { "arquera", 1f }, { "enano", 1f }
        };

        float SideCorrection =>
            SideArtFacing.TryGetValue(_charName, out var f) ? f : 1f;

        Transform _quad;
        Material _mat;
        TokenAnim _anim = TokenAnim.Idle;
        float _animStart;
        bool _oneShot;
        string _charName = "rey";
        float _facing = 1f; // 1 = mira a la derecha (original), -1 = espejo
        string _walkVariant = "walk"; // walk | walk_front | walk_back

        Texture2D _avatar;
        bool _avatarLoaded;
        bool _avatarShown; // en vista cenital la ficha muestra el avatar, no la animación

        // ---- modo 3D: modelo low-poly con rig (toggle en TokenStyleSettings) ----
        bool _is3D;
        GameObject _model;
        Animator _animator;
        bool _hasController; // AnimatorController con árbol idle/walk/victory/defeat
        PlayableGraph _graph;
        float _modelYaw = 180f, _targetYaw = 180f; // orientación del modelo
        Transform _rootPin;        // joint de root motion (solo se fija durante walk)
        Vector3 _rootPinInit;
        Transform _hipsPin;        // pelvis: también se congela durante walk
        Vector3 _hipsPinInit;
        TokenStyleSettings _style; // para el ajuste vertical en vivo
        float _modelBaseLocalY;

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
        static readonly Dictionary<string, string> ModelPrefix = new Dictionary<string, string>
        {
            { "rey", "Rey" } // charId → prefijo de archivos en Resources/Models
        };
        static readonly Dictionary<string, AnimationClip> ClipCache = new Dictionary<string, AnimationClip>();

        AnimationClip ClipFor(TokenAnim anim)
        {
            string prefix = ModelPrefix[_charName];
            string file = anim == TokenAnim.Walk ? $"{prefix}_Walkcycle"
                        : anim == TokenAnim.Victory ? $"{prefix}_Victory"
                        : anim == TokenAnim.Defeat ? $"{prefix}_Defeat"
                        : $"{prefix}_Idle";
            string key = $"Models/{file}";
            if (!ClipCache.TryGetValue(key, out var clip))
            {
                clip = Resources.Load<AnimationClip>(key);
                ClipCache[key] = clip;
            }
            return clip;
        }

        void Play3D(TokenAnim anim)
        {
            if (_animator == null) return;

            // con AnimatorController: el árbol de estados decide las transiciones
            if (_hasController)
            {
                // la caminata se acelera un poco para casar con el ritmo por casillas
                _animator.speed = anim == TokenAnim.Walk ? 1.2f : 1f;
                switch (anim)
                {
                    case TokenAnim.Walk:
                        _animator.SetBool("walking", true);
                        break;
                    case TokenAnim.Victory:
                        _animator.SetBool("walking", false);
                        _animator.SetTrigger("victory");
                        break;
                    case TokenAnim.Defeat:
                        _animator.SetBool("walking", false);
                        _animator.SetTrigger("defeat");
                        break;
                    default:
                        _animator.SetBool("walking", false);
                        break;
                }
                return;
            }

            // respaldo sin controller: reproducir el clip directo con Playables
            var clip = ClipFor(anim);
            if (clip == null && anim != TokenAnim.Idle) clip = ClipFor(TokenAnim.Idle);
            if (clip == null) return;
            if (_graph.IsValid()) _graph.Destroy();
            AnimationPlayableUtilities.PlayClip(_animator, clip, out _graph);
        }

        IEnumerator Back3DToIdle(TokenAnim anim)
        {
            var clip = ClipFor(anim);
            yield return new WaitForSeconds(clip != null ? clip.length : 1.2f);
            if (_anim == anim && _oneShot)
            {
                _oneShot = false;
                SetAnim(TokenAnim.Idle);
            }
        }

        /// <summary>Instancia el modelo, lo escala a la altura del cuerpo y apoya los pies.</summary>
        void SetupModel(GameObject prefab, float bodyH)
        {
            _model = Instantiate(prefab, transform);
            _model.name = "Model3D";
            _animator = _model.GetComponentInChildren<Animator>();
            if (_animator == null) _animator = _model.AddComponent<Animator>();

            // árbol de animaciones (idle/walk/victory/defeat) y caminata in-place
            _animator.applyRootMotion = false;
            var rc = Resources.Load<RuntimeAnimatorController>($"Models/{ModelPrefix[_charName]}Animator");
            if (rc != null)
            {
                _animator.runtimeAnimatorController = rc;
                _hasController = true;
            }

            // anclas anti-desplazamiento (SOLO durante walk; idle/victory/defeat originales):
            // el avance del clip puede venir en cualquier eje local del rig de Maya,
            // así que en walk se congela la traslación completa de root y pelvis
            _rootPin = FindDeep(_model.transform, "GameSkeletonRoot");
            if (_rootPin != null) _rootPinInit = _rootPin.localPosition;
            _hipsPin = FindDeep(_model.transform, "GameSkeletonRoot_M");
            if (_hipsPin != null) _hipsPinInit = _hipsPin.localPosition;

            var rends = _model.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float s = bodyH / Mathf.Max(0.01f, b.size.y);
                _model.transform.localScale = Vector3.one * s;

                // suelo de referencia: los huesos de los dedos del pie (T-pose),
                // más fiable que los bounds del skinned mesh
                float feetWorldY = float.MaxValue;
                foreach (var bone in new[] { "GameSkeletonToesEnd_R", "GameSkeletonToesEnd_L",
                                             "GameSkeletonToes_R", "GameSkeletonToes_L" })
                {
                    var bt = FindDeep(_model.transform, bone);
                    if (bt != null) feetWorldY = Mathf.Min(feetWorldY, bt.position.y);
                }
                b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                if (feetWorldY == float.MaxValue) feetWorldY = b.min.y;

                float groundY = transform.position.y - 0.5f; // superficie del tablero
                _model.transform.position += new Vector3(
                    transform.position.x - b.center.x,
                    groundY - feetWorldY,
                    transform.position.z - b.center.z);
            }
            _modelBaseLocalY = _model.transform.localPosition.y;
        }

        void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        Transform _plate, _plateLabel;
        float _plateBaseY, _labelBaseY;
        bool _plateCached;

        void CachePlate()
        {
            if (_plateCached) return;
            _plateCached = true;
            _plate = transform.Find("NamePlate");
            _plateLabel = transform.Find("NameLabel");
            if (_plate != null) _plateBaseY = _plate.localPosition.y;
            if (_plateLabel != null) _labelBaseY = _plateLabel.localPosition.y;
        }

        /// <summary>Sube o restaura la placa del nombre (y su texto) según la vista.</summary>
        void OffsetPlate(float dy)
        {
            CachePlate();
            if (_plate != null)
            {
                var lp = _plate.localPosition;
                _plate.localPosition = new Vector3(lp.x, _plateBaseY + dy, lp.z);
            }
            if (_plateLabel != null)
            {
                var ll = _plateLabel.localPosition;
                _plateLabel.localPosition = new Vector3(ll.x, _labelBaseY + dy, ll.z);
            }
        }

        Texture2D Avatar
        {
            get
            {
                if (!_avatarLoaded)
                {
                    _avatarLoaded = true;
                    _avatar = Resources.Load<Texture2D>($"Avatars/{_charName}_avatar");
                }
                return _avatar;
            }
        }

        public static CharacterToken Create(string charName = "rey", string displayName = null, Color? nameColor = null)
        {
            var root = new GameObject("CharacterToken");
            var ct = root.AddComponent<CharacterToken>();
            ct._charName = charName;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Sprite";
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(root.transform, false);
            // los pies del sprite quedan a la altura del tablero
            quad.transform.localPosition = new Vector3(0f, ct.WorldHeight / 2f - 0.5f, 0f);

            ct._quad = quad.transform;
            ct._mat = new Material(Shader.Find("Unlit/Transparent"));
            quad.GetComponent<Renderer>().material = ct._mat;

            // ¿esta ficha va en 3D? (toggle TokenStyleSettings en el Bootstrap)
            var style = FindAnyObjectByType<TokenStyleSettings>();
            if (style != null && style.Use3D(charName) && ModelPrefix.ContainsKey(charName))
            {
                var prefab = Resources.Load<GameObject>($"Models/RMo_{ModelPrefix[charName]}");
                if (prefab != null)
                {
                    ct._is3D = true;
                    ct._style = style;
                    quad.SetActive(false); // el quad queda para el avatar en vista Top
                    ct.SetupModel(prefab, style.BodyHeight(charName));
                }
            }

            // Placa con el nombre sobre la cabeza (arte HUD_ingame + tipografía del juego)
            if (!string.IsNullOrEmpty(displayName))
            {
                var plateTex = Resources.Load<Texture2D>("UI/hud_nameplate");
                if (plateTex != null)
                {
                    // banda recoloreada con el color asignado al jugador
                    if (nameColor.HasValue)
                        plateTex = TintedPlate(plateTex, nameColor.Value);
                    var pq = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    pq.name = "NamePlate";
                    Destroy(pq.GetComponent<Collider>());
                    pq.transform.SetParent(root.transform, false);
                    float pw = 1.45f;
                    float ph = pw * ((float)plateTex.height / plateTex.width);
                    // la placa se apoya justo sobre la coronilla del personaje
                    float plateY = ct.HeadTopY + 0.08f + ph / 2f;
                    pq.transform.localPosition = new Vector3(0f, plateY, 0.01f);
                    pq.transform.localScale = new Vector3(pw, ph, 1f);
                    var pmat = new Material(Shader.Find("Unlit/Transparent")) { mainTexture = plateTex };
                    pq.GetComponent<Renderer>().material = pmat;

                    // nombre centrado en el campo azul de la placa
                    CreateNameLabel(root.transform, displayName, new Color(0.96f, 0.90f, 0.72f),
                        new Vector3(0f, plateY - ph * 0.045f, -0.01f), TextAnchor.MiddleCenter);
                }
                else
                {
                    // respaldo: etiqueta con sombra como antes
                    Color c = nameColor ?? Color.white;
                    float labelY = ct.HeadTopY + 0.12f;
                    CreateNameLabel(root.transform, displayName, Color.black, new Vector3(0.025f, labelY - 0.025f, 0.01f));
                    CreateNameLabel(root.transform, displayName, c, new Vector3(0f, labelY, 0f));
                }
            }

            ct.SetAnim(TokenAnim.Idle);
            return ct;
        }

        static readonly Dictionary<(Texture2D, Color), Texture2D> PlateCache
            = new Dictionary<(Texture2D, Color), Texture2D>();

        /// <summary>
        /// Variante de una textura de UI con las zonas azules recoloreadas al color
        /// del jugador (se conserva el sombreado y los ornamentos dorados).
        /// La usan la placa del nombre en el tablero y el HUD del jugador.
        /// </summary>
        public static Texture2D TintedPlate(Texture2D src, Color target)
        {
            if (PlateCache.TryGetValue((src, target), out var cached) && cached != null)
                return cached;
            try
            {
                var px = src.GetPixels32();
                Color.RGBToHSV(target, out float th, out float ts, out float tv);
                for (int i = 0; i < px.Length; i++)
                {
                    var c = px[i];
                    if (c.a == 0) continue;
                    // banda azul: dominancia clara del canal azul sobre rojo y verde
                    if (c.b > c.r + 20 && c.b > c.g + 12)
                    {
                        Color.RGBToHSV(new Color32(c.r, c.g, c.b, 255), out _, out float s, out float v);
                        var nc = (Color32)Color.HSVToRGB(th, s * Mathf.Clamp01(ts + 0.15f), v * Mathf.Clamp01(tv + 0.35f));
                        nc.a = c.a;
                        px[i] = nc;
                    }
                }
                var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
                tex.SetPixels32(px);
                tex.Apply(false, false);
                PlateCache[(src, target)] = tex;
                return tex;
            }
            catch
            {
                return src; // textura no legible: se usa la placa original
            }
        }

        static void CreateNameLabel(Transform parent, string text, Color color, Vector3 localPos,
            TextAnchor anchor = TextAnchor.LowerCenter)
        {
            var go = new GameObject("NameLabel");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * 0.07f;

            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 56;
            tm.characterSize = 0.5f;
            tm.anchor = anchor;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            var font = UIFonts.Title;
            if (font != null)
            {
                tm.font = font;
                go.GetComponent<MeshRenderer>().material = font.material;
                // el material de la fuente pierde el color del TextMesh si no se respeta
                tm.color = color;
            }
        }

        string SheetFor(TokenAnim anim) =>
            anim == TokenAnim.Walk ? _walkVariant : anim.ToString().ToLower();

        Texture2D[] GetFrames(TokenAnim anim)
        {
            string key = $"{_charName}_{SheetFor(anim)}";
            if (!Cache.TryGetValue(key, out var frames))
            {
                frames = new Texture2D[FrameCount];
                for (int i = 0; i < FrameCount; i++)
                    frames[i] = Resources.Load<Texture2D>($"Anims/{key}_{i}");
                Cache[key] = frames;
            }
            return frames;
        }

        public void SetAnim(TokenAnim anim, bool oneShot = false)
        {
            _anim = anim;
            _animStart = Time.time;
            _oneShot = oneShot;
            if (_is3D)
            {
                Play3D(anim);
                if (oneShot) StartCoroutine(Back3DToIdle(anim));
                return;
            }
            FitQuadToAnim();
        }

        public void PlayVictory() => SetAnim(TokenAnim.Victory, true);
        public void PlayDefeat() => SetAnim(TokenAnim.Defeat, true);

        /// <summary>
        /// Elige la animación de caminata según la dirección del paso respecto
        /// a la cámara: lateral (con espejo si va a la izquierda), de frente
        /// (hacia la cámara) o de espaldas (alejándose).
        /// </summary>
        public void SetMoveDirection(Vector3 worldDelta)
        {
            if (_is3D)
            {
                // el modelo gira hacia donde camina (sin variantes de sprite)
                if (worldDelta.sqrMagnitude > 0.0001f)
                    _targetYaw = Mathf.Atan2(worldDelta.x, worldDelta.z) * Mathf.Rad2Deg;
                return;
            }

            var cam = Camera.main;
            if (cam == null || worldDelta.sqrMagnitude < 0.0001f) return;

            float x = Vector3.Dot(worldDelta, cam.transform.right);
            Vector3 camFwd = cam.transform.forward;
            camFwd.y = 0f;
            camFwd.Normalize();
            float z = Vector3.Dot(worldDelta, camFwd);

            string variant;
            float facing = 1f;

            if (Mathf.Abs(x) >= Mathf.Abs(z))
            {
                variant = "walk";              // lateral, espejo según el sentido
                facing = Mathf.Sign(x);
            }
            else
            {
                variant = z < 0f ? "walk_front" : "walk_back";
            }

            if (variant != _walkVariant || !Mathf.Approximately(facing, _facing))
            {
                _walkVariant = variant;
                _facing = facing;
                FitQuadToAnim();
            }
        }

        public void SetWalking(bool walking)
        {
            if (_oneShot) return; // no interrumpir victoria/derrota
            if (walking && _anim != TokenAnim.Walk) SetAnim(TokenAnim.Walk);
            else if (!walking && _anim != TokenAnim.Idle) SetAnim(TokenAnim.Idle);
        }

        void FitQuadToAnim()
        {
            // en vista cenital manda el avatar: no dejar que una animación
            // (caminar, victoria...) re-escale el quad y estire la foto
            if (_avatarShown) return;

            var frames = GetFrames(_anim);
            if (frames[0] == null && _anim != TokenAnim.Idle)
                frames = GetFrames(TokenAnim.Idle);
            var tex = frames[0];
            if (tex == null || _quad == null) return;
            float aspect = (float)tex.width / tex.height;
            // el espejo solo aplica al walk lateral, corregido por la orientación del arte
            float mirror = SheetFor(_anim) == "walk" ? _facing * SideCorrection : 1f;
            _quad.localScale = new Vector3(WorldHeight * aspect * mirror, WorldHeight, 1f);
        }

        /// <summary>Modo 3D: avatar en vista Top y giro suave hacia la marcha.</summary>
        void Update3D()
        {
            bool topView = CameraOrbitController.Instance != null
                        && CameraOrbitController.Instance.Mode == CameraMode.Top;
            if (topView && Avatar != null)
            {
                if (!_avatarShown)
                {
                    _avatarShown = true;
                    _model.SetActive(false);
                    _quad.gameObject.SetActive(true);
                    _mat.mainTexture = Avatar;
                    float aspect = (float)Avatar.width / Avatar.height;
                    const float avatarH = 1.7f;
                    _quad.localScale = new Vector3(avatarH * aspect, avatarH, 1f);
                    CachePlate();
                    float avatarTop = _quad.localPosition.y + avatarH / 2f;
                    float ph = _plate != null ? _plate.localScale.y : 0.44f;
                    OffsetPlate(avatarTop + 0.08f + ph / 2f - _plateBaseY);
                }
                return;
            }
            if (_avatarShown)
            {
                _avatarShown = false;
                _quad.gameObject.SetActive(false);
                _model.SetActive(true);
                OffsetPlate(0f);
            }

            // giro suave del modelo hacia la dirección de la última caminata
            _modelYaw = Mathf.LerpAngle(_modelYaw, _targetYaw, Time.deltaTime * 8f);
            if (_model != null)
            {
                _model.transform.rotation = Quaternion.Euler(0f, _modelYaw, 0f);

                // ajuste vertical en vivo desde el Inspector (Rey Ground Offset)
                float yOff = _style != null ? _style.reyGroundOffset : 0f;
                var mlp = _model.transform.localPosition;
                _model.transform.localPosition = new Vector3(mlp.x, _modelBaseLocalY + yOff, mlp.z);
            }
        }

        void Update()
        {
            if (_is3D) { Update3D(); return; }

            // Vista cenital: el avatar del personaje sustituye a la animación
            bool topView = CameraOrbitController.Instance != null
                        && CameraOrbitController.Instance.Mode == CameraMode.Top;
            if (topView && Avatar != null)
            {
                if (!_avatarShown)
                {
                    _avatarShown = true;
                    _mat.mainTexture = Avatar;
                    float aspect = (float)Avatar.width / Avatar.height;
                    const float avatarH = 1.7f;
                    _quad.localScale = new Vector3(avatarH * aspect, avatarH, 1f);

                    // la placa del nombre pasa a apoyarse sobre el borde superior del avatar
                    CachePlate();
                    float avatarTop = _quad.localPosition.y + avatarH / 2f;
                    float ph = _plate != null ? _plate.localScale.y : 0.44f;
                    OffsetPlate(avatarTop + 0.08f + ph / 2f - _plateBaseY);
                }
                return;
            }
            if (_avatarShown)
            {
                _avatarShown = false;
                FitQuadToAnim();
                _animStart = Time.time;
                OffsetPlate(0f); // la placa vuelve sobre la cabeza del personaje
            }

            var frames = GetFrames(_anim);
            // si al personaje le falta esta animación, usar idle como respaldo
            if (frames[0] == null && _anim != TokenAnim.Idle)
                frames = GetFrames(TokenAnim.Idle);

            int idx = (int)((Time.time - _animStart) / CurrentFrameTime());

            if (_oneShot && idx >= FrameCount)
            {
                _oneShot = false;
                SetAnim(TokenAnim.Idle);
                frames = GetFrames(_anim);
                idx = 0;
            }

            var tex = frames[idx % FrameCount];
            if (tex != null && _mat.mainTexture != tex)
                _mat.mainTexture = tex;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var rot = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);

            if (_is3D)
            {
                // el modelo NO es billboard: solo la placa, el texto y el avatar miran a cámara
                CachePlate();
                bool topView3D = CameraOrbitController.Instance != null
                              && CameraOrbitController.Instance.Mode == CameraMode.Top;
                if (_plate != null) _plate.rotation = rot;
                if (_avatarShown && _quad != null) _quad.rotation = rot;

                if (topView3D && _avatarShown && _quad != null && _plate != null)
                {
                    // En vista cenital un offset en Y es PROFUNDIDAD (la placa caería
                    // encima del avatar). Separamos la placa en el eje "arriba" de la
                    // PANTALLA (cam.up) y la acercamos un poco a la cámara.
                    float ph = _plate.localScale.y;
                    float sep = 1.7f * 0.5f + 0.12f + ph * 0.5f; // medio avatar + margen + media placa
                    _plate.position = _quad.position + cam.transform.up * sep
                                                     - cam.transform.forward * 0.06f;
                    if (_plateLabel != null)
                    {
                        _plateLabel.rotation = rot;
                        _plateLabel.position = _plate.position + rot * new Vector3(0f, -ph * 0.045f, -0.05f);
                    }
                }
                else if (_plate != null && _plateLabel != null)
                {
                    _plateLabel.rotation = rot;
                    // anclado a la placa y SIEMPRE delante de ella hacia la cámara
                    float dy = -_plate.localScale.y * 0.045f; // misma caída que en 2D
                    _plateLabel.position = _plate.position + rot * new Vector3(0f, dy, -0.05f);
                }

                // caminata in-place: durante walk se congela la traslación completa
                // del root y del pelvis (en idle/victory/defeat quedan originales)
                if (_anim == TokenAnim.Walk)
                {
                    if (_rootPin != null) _rootPin.localPosition = _rootPinInit;
                    if (_hipsPin != null) _hipsPin.localPosition = _hipsPinInit;
                }
                return;
            }

            // Billboard: siempre de frente a la cámara
            transform.rotation = rot;
        }
    }
}
